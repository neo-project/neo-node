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
			var from = new UInt160(notificationArray[1].GetByteArray());
			var to = new UInt160(notificationArray[2].GetByteArray());
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
