using Akka.Actor;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Shell
{

    public class Coins
    {
        private Wallet current_wallet;
        private NeoSystem system;
        public static int MAX_CLAIMS_AMOUNT = 50;

        public Coins(Wallet wallet, NeoSystem system)
        {
            this.current_wallet = wallet;
            this.system = system;
        }

        public Fixed8 UnavailableBonus()
        {
            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
            {
                uint height = snapshot.Height + 1;
                Fixed8 unavailable;

                try
                {
                    unavailable = snapshot.CalculateBonus(current_wallet.FindUnspentCoins().Where(p => p.Output.AssetId.Equals(Blockchain.GoverningToken.Hash)).Select(p => p.Reference), height);
                }
                catch (Exception)
                {
                    unavailable = Fixed8.Zero;
                }

                return unavailable;
            }
        }


        public Fixed8 AvailableBonus()
        {
            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
            {
                return snapshot.CalculateBonus(current_wallet.GetUnclaimedCoins().Select(p => p.Reference));
            }
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

            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
            {
                ClaimTransaction tx = new ClaimTransaction
                {
                    Claims = claims.Take(MAX_CLAIMS_AMOUNT).ToArray(),
                    Attributes = new TransactionAttribute[0],
                    Inputs = new CoinReference[0],
                    Outputs = new[]
                    {
                        new TransactionOutput
                        {
                            AssetId = Blockchain.UtilityToken.Hash,
                            Value = snapshot.CalculateBonus(claims.Take(MAX_CLAIMS_AMOUNT)),
                            ScriptHash = current_wallet.GetChangeAddress()
                        }
                    }

                };

                return (ClaimTransaction)SignTransaction(tx);
            }
        }


        public ClaimTransaction[] ClaimAll()
        {

            if (this.AvailableBonus() == Fixed8.Zero)
            {
                Console.WriteLine($"no gas to claim");
                return null;
            }

            CoinReference[] claims = current_wallet.GetUnclaimedCoins().Select(p => p.Reference).ToArray();
            if (claims.Length == 0) return null;

            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
            {
                int claim_count = (claims.Length - 1) / MAX_CLAIMS_AMOUNT + 1;
                List<ClaimTransaction> txs = new List<ClaimTransaction>();
                if (claim_count > 1)
                {
                    Console.WriteLine($"total claims: {claims.Length}, processing(0/{claim_count})...");
                }
                for (int i = 0; i < claim_count; i++)
                {
                    if (i > 0)
                    {
                        Console.WriteLine($"{i * MAX_CLAIMS_AMOUNT} claims processed({i}/{claim_count})...");
                    }
                    ClaimTransaction tx = new ClaimTransaction
                    {
                        Claims = claims.Skip(i * MAX_CLAIMS_AMOUNT).Take(MAX_CLAIMS_AMOUNT).ToArray(),
                        Attributes = new TransactionAttribute[0],
                        Inputs = new CoinReference[0],
                        Outputs = new[]
                        {
                            new TransactionOutput
                            {
                                AssetId = Blockchain.UtilityToken.Hash,
                                Value = snapshot.CalculateBonus(claims.Skip(i * MAX_CLAIMS_AMOUNT).Take(MAX_CLAIMS_AMOUNT)),
                                ScriptHash = current_wallet.GetChangeAddress()
                            }
                        }
                    };

                    if ((tx = (ClaimTransaction)SignTransaction(tx)) != null)
                    {
                        txs.Add(tx);
                    }
                    else
                    {
                        break;
                    }
                }

                return txs.ToArray();
            }
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

                bool relay_result = system.Blockchain.Ask<RelayResultReason>(tx).Result == RelayResultReason.Succeed;

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
