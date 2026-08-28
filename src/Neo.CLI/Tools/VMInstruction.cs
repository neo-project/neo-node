// Copyright (C) 2015-2026 The Neo Project.
//
// VMInstruction.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.VM;
using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.CLI;

[DebuggerDisplay("OpCode={OpCode}, OperandSize={OperandSize}")]
internal sealed class VMInstruction : IEnumerable<VMInstruction>
{
    private const int OpCodeSize = 1;

    public int Position { get; private init; }
    public OpCode OpCode { get; private init; }
    public ReadOnlyMemory<byte> Operand { get; private init; }
    public int OperandSize { get; private init; }
    public int OperandPrefixSize { get; private init; }

    private static readonly int[] s_operandSizeTable = new int[256];
    private static readonly int[] s_operandSizePrefixTable = new int[256];

    private readonly ReadOnlyMemory<byte> _script;

    public VMInstruction(ReadOnlyMemory<byte> script, int start = 0)
    {
        if (script.IsEmpty)
            throw new Exception("Bad Script.");

        var opcode = (OpCode)script.Span[start];

        if (Enum.IsDefined(opcode) == false)
            throw new InvalidDataException($"Invalid opcode at Position: {start}.");

        OperandPrefixSize = s_operandSizePrefixTable[(int)opcode];
        OperandSize = OperandPrefixSize switch
        {
            0 => s_operandSizeTable[(int)opcode],
            1 => script.Span[start + 1],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(script.Span[(start + 1)..]),
            4 => unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(script.Span[(start + 1)..])),
            _ => throw new InvalidDataException($"Invalid opcode prefix at Position: {start}."),
        };

        OperandSize += OperandPrefixSize;

        if (start + OperandSize + OpCodeSize > script.Length)
            throw new IndexOutOfRangeException("Operand size exceeds end of script.");

        Operand = script.Slice(start + OpCodeSize, OperandSize);

        _script = script;
        OpCode = opcode;
        Position = start;
    }

    static VMInstruction()
    {
        foreach (var field in typeof(OpCode).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = field.GetCustomAttribute<OperandSizeAttribute>();
            if (attr == null) continue;

            var index = (uint)(OpCode)field.GetValue(null)!;
            s_operandSizeTable[index] = attr.Size;
            s_operandSizePrefixTable[index] = attr.SizePrefix;
        }
    }

    public IEnumerator<VMInstruction> GetEnumerator()
    {
        var nip = Position + OperandSize + OpCodeSize;
        yield return this;

        VMInstruction? instruct;
        for (var ip = nip; ip < _script.Length; ip += instruct.OperandSize + OpCodeSize)
            yield return instruct = new VMInstruction(_script, ip);
    }

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public override string ToString()
    {
        if (OperandSize == 0)
            return string.Format("{0:X04} {1}", Position, OpCode);
        return string.Format("{0:X04} {1,-10}{2}", Position, OpCode, DecodeOperand());
    }

    public T AsToken<T>(uint index = 0)
        where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();

        if (size > OperandSize)
            throw new ArgumentOutOfRangeException(nameof(T), $"SizeOf {typeof(T).FullName} is too big for operand. OpCode: {OpCode}.");
        if (size + index > OperandSize)
            throw new ArgumentOutOfRangeException(nameof(index), $"SizeOf {typeof(T).FullName} + {index} is too big for operand. OpCode: {OpCode}.");

        var bytes = Operand[..OperandSize].ToArray();
        return Unsafe.As<byte, T>(ref bytes[index]);
    }

    public string DecodeOperand()
    {
        var operand = Operand[OperandPrefixSize..].ToArray();
        var asStr = Encoding.UTF8.GetString(operand);
        var readable = asStr.All(char.IsAsciiLetterOrDigit);

        return OpCode switch
        {
            OpCode.JMP or
            OpCode.JMPIF or
            OpCode.JMPIFNOT or
            OpCode.JMPEQ or
            OpCode.JMPNE or
            OpCode.JMPGT or
            OpCode.JMPLT or
            OpCode.CALL or
            OpCode.ENDTRY => $"[{checked(Position + AsToken<byte>()):X08}]",
            OpCode.JMP_L or
            OpCode.JMPIF_L or
            OpCode.PUSHA or
            OpCode.JMPIFNOT_L or
            OpCode.JMPEQ_L or
            OpCode.JMPNE_L or
            OpCode.JMPGT_L or
            OpCode.JMPLT_L or
            OpCode.CALL_L or
            OpCode.ENDTRY_L => $"[{checked(Position + AsToken<int>()):X08}]",
            OpCode.TRY => $"[{AsToken<byte>():X02}, {AsToken<byte>(1):X02}]",
            OpCode.INITSLOT => $"{AsToken<byte>()}, {AsToken<byte>(1)}",
            OpCode.TRY_L => $"[{checked(Position + AsToken<int>()):X08}, {checked(Position + AsToken<int>()):X08}]",
            OpCode.CALLT => $"[{checked(Position + AsToken<ushort>()):X08}]",
            OpCode.NEWARRAY_T or
            OpCode.ISTYPE or
            OpCode.CONVERT => $"{AsToken<byte>():X02}",
            OpCode.STLOC or
            OpCode.LDLOC or
            OpCode.LDSFLD or
            OpCode.STSFLD or
            OpCode.LDARG or
            OpCode.STARG or
            OpCode.INITSSLOT => $"{AsToken<byte>()}",
            OpCode.PUSHINT8 => FormatInteger(AsToken<sbyte>()),
            OpCode.PUSHINT16 => FormatInteger(AsToken<short>()),
            OpCode.PUSHINT32 => FormatInteger(AsToken<int>()),
            OpCode.PUSHINT64 => FormatInteger(AsToken<long>()),
            OpCode.PUSHINT128 or
            OpCode.PUSHINT256 => $"{new BigInteger(operand)}",
            OpCode.SYSCALL => $"[{ApplicationEngine.Services[Unsafe.As<byte, uint>(ref operand[0])].Name}]",
            OpCode.PUSHDATA1 or
            OpCode.PUSHDATA2 or
            OpCode.PUSHDATA4 => FormatPushData(operand),
            _ => IsReadableText(asStr) ? $"\"{asStr}\"" : $"{Convert.ToHexString(operand)}",
        };
    }

    private static string FormatPushData(byte[] operand)
    {
        var hex = Convert.ToHexString(operand);
        var asStr = Encoding.UTF8.GetString(operand);
        if (IsReadableText(asStr))
            return $"{hex} // {asStr}";

        if (TryDecodeEcPoint(operand, out var point))
            return $"{hex} // {point}";

        if (operand.Length == UInt160.Length)
            return $"{hex} // {new UInt160(operand)}";

        if (operand.Length == UInt256.Length)
            return $"{hex} // {new UInt256(operand)}";

        if (operand.Length == 4 && TryFormatUnixTimestamp(BinaryPrimitives.ReadUInt32LittleEndian(operand), out var ts32))
            return $"{hex} // {ts32}";

        if (operand.Length == 8 && TryFormatUnixTimestamp(BinaryPrimitives.ReadInt64LittleEndian(operand), out var ts64))
            return $"{hex} // {ts64}";

        return hex;
    }

    private static string FormatInteger(long value)
        => TryFormatUnixTimestamp(value, out var ts) ? $"{value} // {ts}" : value.ToString(CultureInfo.InvariantCulture);

    private static bool IsReadableText(string value)
        => value.Length > 0 && value.Any(char.IsAsciiLetter) && value.All(static c => char.IsAscii(c) && !char.IsControl(c));

    private static bool TryDecodeEcPoint(byte[] operand, out ECPoint point)
    {
        point = null!;
        if (operand.Length is not (33 or 65))
            return false;
        if (operand[0] is not (0x02 or 0x03 or 0x04))
            return false;
        try
        {
            point = ECPoint.DecodePoint(operand, ECCurve.Secp256r1);
            return true;
        }
        catch (FormatException)
        {
            try
            {
                point = ECPoint.DecodePoint(operand, ECCurve.Secp256k1);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Unix seconds in 2000–2100, or unix milliseconds in that same window.
    /// </summary>
    private static bool TryFormatUnixTimestamp(long value, out string text)
    {
        const long UnixSeconds2000 = 946_684_800;
        const long UnixSeconds2100 = 4_102_444_800;
        const long UnixMilliseconds2000 = 946_684_800_000;
        const long UnixMilliseconds2100 = 4_102_444_800_000;

        if (value >= UnixSeconds2000 && value <= UnixSeconds2100)
        {
            text = DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            return true;
        }

        if (value >= UnixMilliseconds2000 && value <= UnixMilliseconds2100)
        {
            text = DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            return true;
        }

        text = null!;
        return false;
    }
}
