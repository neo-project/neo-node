using Akka.Actor;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Linq;

namespace Neo.Shell
{

    public class Coins
    {
        private Wallet current_wallet;
        private NeoSystem system;

        public Coins(Wallet wallet, NeoSystem system)
        {
            this.current_wallet = wallet;
            this.system = system;
        }

        public Fixed8 UnavailableBonus()
        {
            uint height = Blockchain.Singleton.Snapshot.Height + 1;
            Fixed8 unavailable;

            try
            {
                unavailable = Blockchain.Singleton.Snapshot.CalculateBonus(current_wallet.FindUnspentCoins().Where(p => p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)).Select(p => p.Reference), height);
            }
            catch (Exception)
            {
                unavailable = Fixed8.Zero;
            }

            return unavailable;
        }


        public Fixed8 AvailableBonus()
        {
            return Blockchain.Singleton.Snapshot.CalculateBonus(current_wallet.GetUnclaimedCoins().Select(p => p.Reference));
        }


        public ClaimTransaction Claim()
        {

            if (this.AvailableBonus() == Fixed8.Zero)
            {
                Console.WriteLine($"no gas to claim");
                return null;
            }

            CoinReference[] claims = current_wallet.GetUnclaimedCoins().Select(p => p.Reference).ToArray();
            if (claims.Length == 0) return null;

            ClaimTransaction tx = new ClaimTransaction
            {
                Claims = claims,
                Attributes = new TransactionAttribute[0],
                Inputs = new CoinReference[0],
                Outputs = new[]
                {
                    new TransactionOutput
                    {
                        AssetId = Blockchain.UtilityToken.Hash,
                        Value = Blockchain.Singleton.Snapshot.CalculateBonus(claims),
                        ScriptHash = current_wallet.GetChangeAddress()
                    }
                }

            };

            return (ClaimTransaction)SignTransaction(tx);
        }


        private Transaction SignTransaction(Transaction tx)
        {
            if (tx == null)
            {
                Console.WriteLine($"no transaction specified");
                return null;
            }
            ContractParametersContext context;

            try
            {
                context = new ContractParametersContext(tx);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine($"unsynchronized block");

                return null;
            }

            current_wallet.Sign(context);

            if (context.Completed)
            {
                context.Verifiable.Witnesses = context.GetWitnesses();
                current_wallet.ApplyTransaction(tx);

                bool relay_result = system.Blockchain.Ask<Blockchain.RelayResult>(new Blockchain.NewTransaction { Transaction = tx }).Result.Reason == RelayResultReason.Succeed;

                if (relay_result)
                {
                    return tx;
                }
                else
                {
                    Console.WriteLine($"Local Node could not relay transaction: {tx.Hash.ToString()}");
                }
            }
            else
            {
                Console.WriteLine($"Incomplete Signature: {context.ToString()}");
            }

            return null;
        }
    }
}
