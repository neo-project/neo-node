using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.Cli.Extensions
{
	public static class CLIHelper
	{
		
		public static string ToCLIString(this NotifyEventArgs notification)
		{
			string output = "";
			var vmArray = (Neo.VM.Types.Array)notification.State;
			var notificationName = "";
			if (vmArray.Count > 0)
			{
				notificationName = vmArray[0].GetString();
			}

			var adapter = NotificationCLIAdapter.GetCliStringAdapter(notificationName);
			output += adapter(vmArray);

			return output;
		}

		public static string ToCLIString(this Block block)
		{
			string output = "";
			output += $"Hash: {block.Hash}\n";
			output += $"Index: {block.Index}\n";
			output += $"Size: {block.Size}\n";
			output += $"PreviousBlockHash: {block.PrevHash}\n";
			output += $"MerkleRoot: {block.MerkleRoot}\n";
			output += $"Time: {block.Timestamp}\n";
			output += $"NextConsensus: {block.NextConsensus}\n";
			output += $"Transactions:\n";
			foreach (Transaction t in block.Transactions)
			{
				//output += $"\tHash: {t.Hash}\n";
				//output += $"\tNetFee: {t.NetworkFee}\n";
				//output += $"\tSysFee: {t.SystemFee}\n";
				//output += $"\tSender: {t.Sender}\n";
				//output += $"\tScript: {t.Script.ToHexString()}\n";
				output += $"{t.ToCLIString()}";
				output += $"\n";
			}
			output += $"Witnesses:\n";
			output += $"\tInvocation: {block.Witness.InvocationScript.ToHexString()}\n";
			output += $"\tVerification: {block.Witness.VerificationScript.ToHexString()}\n";
			return output;
		}

		public static string ToCLIString(this Transaction t)
		{
			var supportedMethods = InteropService.SupportedMethods();
			string output = "";
			output += $"Hash: {t.Hash}\n";
			output += $"NetFee: {t.NetworkFee}\n";
			output += $"SysFee: {t.SystemFee}\n";
			output += $"Sender: {t.Sender}\n";
			output += $"Script:\n";
			for (int i = 0; i < t.Script.Length; i++)
			{
				OpCode currentOpCode = (OpCode)t.Script[i];
				output += $"\t{currentOpCode.ToString()}\n";
				if (currentOpCode == OpCode.SYSCALL)
				{
					var interop = t.Script.Skip(i+1).Take(4).ToArray();
					var callNumber = BitConverter.ToUInt32(interop);
					var bytes = BitConverter.GetBytes(InteropService.Neo_Native_Deploy);
					var methodName = supportedMethods[callNumber];
					output += $"\t{methodName}\n";
					i = i + 4;
				}
			}
			
			return output;
		}

		public static string ToCLIString(this ContractState c)
		{
			string output = "";
			output += $"Hash: {c.ScriptHash}\n";
			output += $"EntryPoint: \n";
			output += $"\tName: {c.Manifest.Abi.EntryPoint.Name}\n";
			output += $"\tParameters: \n";
			foreach (var parameter in c.Manifest.Abi.EntryPoint.Parameters)
			{
				output += $"\t\tName: {parameter.Name}\n";
				output += $"\t\tType: {parameter.Type}\n";
			}

			output += $"Methods: \n";
			foreach (var method in c.Manifest.Abi.Methods)
			{
				output += $"\tName: {method.Name}\n";
				output += $"\tReturn Type: {method.ReturnType}\n";
				if (method.Parameters.Length > 0)
				{
					output += $"\tParameters: \n";
					foreach (var parameter in method.Parameters)
					{
						output += $"\t\tName: {parameter.Name}\n";
						output += $"\t\tType: {parameter.Type}\n";
					}
				}
				
			}

			output += $"Events: \n";
			foreach (var abiEvent in c.Manifest.Abi.Events)
			{
				output += $"\tName: {abiEvent.Name}\n";
				if (abiEvent.Parameters.Length > 0)
				{
					output += $"\tParameters: \n";
					foreach (var parameter in abiEvent.Parameters)
					{
						output += $"\t\tName: {parameter.Name}\n";
						output += $"\t\tType: {parameter.Type}\n";
					}
				}
			}

			return output;
		}
	}
}
