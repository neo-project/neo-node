using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.Cli.Extensions
{
	public static class CLIHelper
	{
		public static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

		public static string DisassembleScript(byte[] script)
		{
			var supportedMethods = InteropService.SupportedMethods();
			string output = "";
			var outputAppends = new List<string>();
			for (int i = 0; i < script.Length; i++)
			{
				OpCode currentOpCode = (OpCode)script[i];

				if (currentOpCode == OpCode.SYSCALL)
				{
					var interop = script.Skip(i + 1).Take(4).ToArray();
					var callNumber = BitConverter.ToUInt32(interop);
					var bytes = BitConverter.GetBytes(InteropService.Neo_Native_Deploy);
					var methodName = supportedMethods[callNumber];
					outputAppends.Add($"\t{methodName}\n");
					i = i + 4;
				}
				else if (currentOpCode <= OpCode.PUSHBYTES75)
				{
					var byteArraySize = (int)currentOpCode;
					var byteArray = script.Skip(i + 1).Take(byteArraySize).ToArray();
					var hexString = byteArray.ToHexString();
					if (byteArraySize == 20)
					{
						var scriptHash = new UInt160(byteArray);
						hexString = scriptHash.ToString();
					}

					outputAppends.Add($"\t{hexString}\n");
					i = i + byteArraySize;
				}
				//TODO Finish (I don't have use for this yet)
				//else if (currentOpCode == OpCode.PUSHDATA1)
				//{
				//	byte byteArraySize = script.Skip(i + 1).Take(1).First();
				//	var byteArray = script.Skip(i + 1).Take(byteArraySize).ToArray();
				//	var hexString = byteArray.ToHexString();
				//	outputAppends.Add($"\t{hexString}\n");
				//	i = i + 1 + byteArraySize;
				//}

				outputAppends.Add($"\t{currentOpCode.ToString()}\n");
			}

			for (int i = outputAppends.Count - 1; i >= 0; i--)
			{
				output += outputAppends[i];
			}

			return output;
		}

		public static string ToCLIString(this Cosigner cosigner)
		{
			string output = $"\tAccount: {cosigner.Account}\n";
			
			output += "\tScope:\t";
			if (cosigner.Scopes.HasFlag(WitnessScope.CalledByEntry))
			{
				output += $"CalledByEntry\t";
			}
			if (cosigner.Scopes.HasFlag(WitnessScope.CustomContracts))
			{
				output += $"CustomContract\t";
			}
			if (cosigner.Scopes.HasFlag(WitnessScope.CustomGroups))
			{
				output += $"CustomGroup\t";
			}

			output += "\n";

			if (cosigner.AllowedContracts != null && cosigner.AllowedContracts.Length > 0)
			{
				output += "Allowed contracts: \n";
				foreach (var allowedContract in cosigner.AllowedContracts)
				{
					output += $"\t{allowedContract.ToString()}\n";
				}
			}

			if (cosigner.AllowedGroups != null && cosigner.AllowedGroups.Length > 0)
			{
				output += "Allowed groups: \n";
				foreach (var allowedGroup in cosigner.AllowedGroups)
				{
					output += $"\t{allowedGroup.ToString()}\n";
				}
			}
			
			return output;
		}

		public static string ToCLIString(this Witness witness)
		{
			string output = "";
			output += $"Invocation: \n";
			output += DisassembleScript(witness.InvocationScript);
			output += $"Verification: \n";
			output += DisassembleScript(witness.VerificationScript);
			return output;
		}


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
				output += $"{t.ToCLIString(block.Timestamp)}";
				output += $"\n";
			}
			output += $"Witnesses:\n";
			output += $"\tInvocation: {block.Witness.InvocationScript.ToHexString()}\n";
			output += $"\tVerification: {block.Witness.VerificationScript.ToHexString()}\n";
			return output;
		}

		public static string ToCLIString(this Transaction t, ulong blockTimestamp = 0)
		{	
			string output = "";
			output += $"Hash: {t.Hash}\n";
			if (blockTimestamp > 0)
			{
				var blockTime = UnixEpoch.AddMilliseconds(blockTimestamp);
				blockTime = TimeZoneInfo.ConvertTimeFromUtc(blockTime, TimeZoneInfo.Local);
				output += $"Timestamp: {blockTime.ToShortDateString()} {blockTime.ToLongTimeString()}\n";
			}
			output += $"NetFee: {t.NetworkFee}\n";
			output += $"SysFee: {t.SystemFee}\n";
			output += $"Sender: {t.Sender.ToAddress()}\n";
			if (t.Cosigners != null && t.Cosigners.Length > 0)
			{
				output += $"Cosigners:\n";
				foreach (var cosigner in t.Cosigners)
				{
					output += cosigner.ToCLIString();
				}
			}
			

			output += $"Script:\n";
			output += DisassembleScript(t.Script);
		
			output += "Witnesses: \n";
			foreach (var witness in t.Witnesses)
			{
				output += $"{witness.ToCLIString()}";
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
