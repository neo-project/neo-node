using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Text;

namespace Neo.Cli.Extensions
{
	class NotificationCLIAdapter
	{
		private static Dictionary<string, Func<VM.Types.Array, string>> notificationAdapters = new Dictionary<string, Func<VM.Types.Array, string>>()
		{
			{"transfer", TransferNotificationCLIStringAdapter},
			{"Transfer", TransferNotificationCLIStringAdapter}
		};
			
		private static string TransferNotificationCLIStringAdapter(VM.Types.Array notificationArray)
		{
			var fromBytes = notificationArray[1].GetByteArray();
			var toBytes = notificationArray[2].GetByteArray();
			var from = fromBytes.Length > 0 ? new UInt160(fromBytes) : UInt160.Zero;
			var to = toBytes.Length > 0 ? new UInt160(toBytes) : UInt160.Zero;
			var amount = notificationArray[3].GetBigInteger();
			
			var output = $"transfer / {from.ToAddress()} / {to.ToAddress()} / {amount}";
			
			return output;
		}

		private static string DefaultCLIStringAdapter(VM.Types.Array notificationArray)
		{
			var output = notificationArray.ToString();
			return output;
		}

		public static Func<VM.Types.Array, string> GetCliStringAdapter(string methodName)
		{
			return notificationAdapters.ContainsKey(methodName) ?  notificationAdapters[methodName] : DefaultCLIStringAdapter;
		}

	}
}
