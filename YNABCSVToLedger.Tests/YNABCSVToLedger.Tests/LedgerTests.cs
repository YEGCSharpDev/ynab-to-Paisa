﻿namespace LedgerTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.Serialization;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using YNABCSVToLedger;

    /// <summary>
    /// A list of unit tests for the conversion of YNAB data to plain text accounting text
    /// </summary>
    public class Tests
    {
        /// <summary>
        /// The en-US culture used by most of the unit tests
        /// </summary>
        private CultureInfo unitedStatesCulture = new CultureInfo("en-US");

        /// <summary>
        /// A mapping of the account types
        /// </summary>
        private IDictionary<string, string> accountTypes = new Dictionary<string, string>()
        {
            ["Checking Account"] = "Assets",
            ["Savings Account"] = "Assets",
            ["Compte Chèque"] = "Assets",
        };

        /// <summary>
        /// A spot test of a basic income non-split transaction
        /// </summary>
        [Test]
        public void EnsureASimpleIncomeTransactionWorks()
        {
            CSVLineItem t = new CSVLineItem()
            {
                Account = "Checking Account",
                Category = "Income:Available this month",
                SubCategory = "Available this month",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$1,234.56",
                Cleared = "C",
                MasterCategory = "Income",
                Memo = "January Bonus",
                Outflow = "$0.00",

                Payee = "Megacorp LLC",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t },
                this.accountTypes,
                true,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01 * Megacorp LLC
 ; January Bonus
 Assets:Checking Account  1,234.56 USD
 Income:Megacorp LLC  -1,234.56 USD
";

            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// A spot test of a basic income non-split transaction
        /// </summary>
        [Test]
        public void EnsureASimpleExpenseTransactionWorks()
        {
            CSVLineItem t = new CSVLineItem()
            {
                Account = "Checking Account",
                Category = "Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Expenses",
                Memo = "Food for party",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t },
                this.accountTypes,
                true,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01 * Megacorp LLC
 ; Food for party
 Assets:Checking Account  -42.42 USD
 Expenses:Expenses:Groceries  42.42 USD
";

            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// Test whether or not the 'useClear' field is followed, and that the correct clear fields are used
        /// </summary>
        [Test]
        public void DontUseClearAsterisk()
        {
            CSVLineItem clearedTransaction = new CSVLineItem()
            {
                Account = "Checking Account",
                Category = "Income:Available this month",
                SubCategory = "Available this month",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$1,234.56",
                Cleared = "C",
                MasterCategory = "Income",
                Memo = "January Bonus",
                Outflow = "$0.00",

                Payee = "Megacorp LLC",
            };

            CSVLineItem unclearedTransaction = new CSVLineItem()
            {
                Account = "Checking Account",
                Category = "Income:Available this month",
                SubCategory = "Available this month",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$1,234.56",
                Cleared = "U",
                MasterCategory = "Income",
                Memo = "January Bonus",
                Outflow = "$0.00",

                Payee = "Megacorp LLC",
            };

            var transactionsNoClear = Program.GroupLineItems(
                new List<CSVLineItem>() { clearedTransaction },
                this.accountTypes,
                false,
                this.unitedStatesCulture);
            string noClearExpected =
@"2019-01-01 Megacorp LLC
 ; January Bonus
 Assets:Checking Account  1,234.56 USD
 Income:Megacorp LLC  -1,234.56 USD
";

            var transactionsWithClear = Program.GroupLineItems(
                new List<CSVLineItem>() { clearedTransaction },
                this.accountTypes,
                true,
                this.unitedStatesCulture);
            string clearExpected =
@"2019-01-01 * Megacorp LLC
 ; January Bonus
 Assets:Checking Account  1,234.56 USD
 Income:Megacorp LLC  -1,234.56 USD
";

            var unclearedTransactions = Program.GroupLineItems(
                new List<CSVLineItem>() { unclearedTransaction },
                this.accountTypes,
                true,
                this.unitedStatesCulture);
            string notClearExpected =
@"2019-01-01 ! Megacorp LLC
 ; January Bonus
 Assets:Checking Account  1,234.56 USD
 Income:Megacorp LLC  -1,234.56 USD
";
            Assert.That(Program.CreateLedger(transactionsNoClear), Is.EqualTo(noClearExpected));
            Assert.That(Program.CreateLedger(transactionsWithClear), Is.EqualTo(clearExpected));
            Assert.That(Program.CreateLedger(unclearedTransactions), Is.EqualTo(notClearExpected));

        }

        /// <summary>
        /// Test to ensure the check number is included and in the correct place
        /// </summary>
        [Test]
        public void CheckNumberTest()
        {
            CSVLineItem t = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Income:Available this month",
                SubCategory = "Available this month",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$1,234.56",
                Cleared = "C",
                MasterCategory = "Income",
                Memo = "January Bonus",
                Outflow = "$0.00",

                Payee = "Megacorp LLC",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t },
                this.accountTypes,
                false,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01 Megacorp LLC
 ; January Bonus
 Assets:Checking Account  1,234.56 USD
 Income:Megacorp LLC  -1,234.56 USD
";

            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// Test to ensure a basic transfer transaction is correctly processed
        /// </summary>
        [Test]
        public void AccountTransferTest()
        {
            CSVLineItem t = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = string.Empty,
                SubCategory = string.Empty,
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = string.Empty,
                Memo = "Saving up for college",
                Outflow = "$42.42",

                Payee = "Transfer : Savings Account",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t },
                this.accountTypes,
                false,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01
 ; Saving up for college
 Assets:Checking Account  -42.42 USD
 Assets:Savings Account  42.42 USD
";

            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// Test to ensure transfers aren't included twice.
        /// YNAB includes a transfer transaction in both accounts.
        /// It should be excluded with double entry book keeping
        /// </summary>
        [Test]
        public void EnsureTransfersArentDuplicated()
        {
            CSVLineItem t = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = string.Empty,
                SubCategory = string.Empty,
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = string.Empty,
                Memo = "Saving up for college",
                Outflow = "$42.42",

                Payee = "Transfer : Savings Account",
            };

            CSVLineItem t2 = new CSVLineItem()
            {
                Account = "Savings Account",

                Category = string.Empty,
                SubCategory = string.Empty,
                Date = new DateTime(2019, 1, 1),
                Inflow = "$42.42",
                Cleared = "C",
                MasterCategory = string.Empty,
                Memo = "Saving up for college",
                Outflow = "$0.00",

                Payee = "Transfer : Checking Account",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t, t2 },
                this.accountTypes,
                false,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01
 ; Saving up for college
 Assets:Checking Account  -42.42 USD
 Assets:Savings Account  42.42 USD
";
            string actual = Program.CreateLedger(grouped);
            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// Test to ensure the check number is included and in the correct place
        /// </summary>
        [Test]
        public void TransactionsWithFlagAreSupported()
        {
            CSVLineItem t1 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Flag = "Blue",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 2/2) Meat",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            CSVLineItem t2 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Flag = "Blue",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 1/2) Produce",
                Outflow = "$12.42",

                Payee = "Megacorp LLC",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t1, t2 },
                this.accountTypes,
                true,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01 * Megacorp LLC
 ; :Blue:
 Assets:Checking Account  -54.84 USD
 Expenses:Every Expenses:Groceries  12.42 USD ; Produce
 Expenses:Every Expenses:Groceries  42.42 USD ; Meat
";

            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// Test split payments and ensure the line items are sorted in ascending order by amount
        /// </summary>
        [Test]
        public void SplitPaymentsAreSortedByAmount()
        {
            CSVLineItem t1 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 2/2) Meat",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            CSVLineItem t2 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 1/2) Produce",
                Outflow = "$12.42",

                Payee = "Megacorp LLC",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t1, t2 },
                this.accountTypes,
                true,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01 * Megacorp LLC
 Assets:Checking Account  -54.84 USD
 Expenses:Every Expenses:Groceries  12.42 USD ; Produce
 Expenses:Every Expenses:Groceries  42.42 USD ; Meat
";

            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests the behavior of the MemoWithoutSplit field to ensure it handles all 3 cases appropriately
        /// </summary>
        [Test]
        public void SplitMemoBehavesAsExpected()
        {
            CSVLineItem t1 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 1/2) This is a test ",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            CSVLineItem t2 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 2/2)",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            CSVLineItem t3 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "A regular old transaction",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            var transactions = Program.GroupLineItems(
                new List<CSVLineItem>() { t1, t2, t3 },
                this.accountTypes,
                true,
                this.unitedStatesCulture);

            Assert.That(transactions[0].LineItems[0].MemoWithoutSplit, Is.EqualTo("This is a test"));
            Assert.That(transactions[0].LineItems[1].MemoWithoutSplit, Is.Null);
            Assert.That(transactions[1].LineItems[0].MemoWithoutSplit, Is.EqualTo("A regular old transaction"));
        }

        /// <summary>
        /// Test that split payments with multiple payees is supported correctly
        /// </summary>
        //// [Test]
        public void SplitPaymentsWithMultiplePayees()
        {
            CSVLineItem t1 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 2/2) Meat",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            CSVLineItem t2 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "(Split 1/2) Produce",
                Outflow = "$12.42",

                Payee = "Microcorp LLC",
            };

            var grouped = Program.GroupLineItems(
                new List<CSVLineItem>() { t1, t2 },
                this.accountTypes,
                false,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01 *
 Assets:Checking Account  -$54.84
 Expenses:Every Expenses:Groceries  $12.42 ; Payee: Megacorp LLC, Produce
 Expenses:Every Expenses:Groceries  $42.42 ; Payee: Microcorp LLC, Meat";

            Assert.That(Program.CreateLedger(grouped),Is.EqualTo(expected));
        }

        /// <summary>
        /// Ensures that the output of the application has a new line
        /// This is to ensure the file will end with a newline character.
        /// <see href="https://github.com/ledger/ledger/issues/516" />
        /// </summary>
        [Test]
        public void EnsureOutputEndsWithNewLine()
        {
            CSVLineItem t1 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Everyday Expenses:Groceries",
                SubCategory = "Groceries",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "For the picnic",
                Outflow = "$42.42",

                Payee = "Megacorp LLC",
            };

            CSVLineItem t2 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Monthly Bills:Phone Bill",
                SubCategory = "Phone Bill",
                Date = new DateTime(2019, 1, 1),
                Inflow = "$0.00",
                Cleared = "C",
                MasterCategory = "Every Expenses",
                Memo = "Confirmation Number: 12345",
                Outflow = "$42.43",

                Payee = "Megamobile LLC",
            };

            var transactions = Program.GroupLineItems(
                new List<CSVLineItem>() { t1, t2 },
                this.accountTypes,
                true,
                this.unitedStatesCulture);
            string expected =
@"2019-01-01 * Megacorp LLC
 ; For the picnic
 Assets:Checking Account  -42.42 USD
 Expenses:Every Expenses:Groceries  42.42 USD

2019-01-01 * Megamobile LLC
 ; Confirmation Number: 12345
 Assets:Checking Account  -42.43 USD
 Expenses:Every Expenses:Phone Bill  42.43 USD
";
            string result = Program.CreateLedger(transactions);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result.EndsWith(Environment.NewLine));
        }

        /// <summary>
        /// Tests the behavior of the MemoWithoutSplit field to ensure it handles all 3 cases appropriately
        /// </summary>
        [Test]
        public void NonEnUsCultureIsSupported()
        {
            CSVLineItem t1 = new CSVLineItem()
            {
                Account = "Compte Chèque",

                Category = "Dépenses journalières:Supermarché",
                SubCategory = "Supermarché",
                Date = new DateTime(2019, 1, 1),
                Inflow = "€0,00",
                Cleared = "C",
                MasterCategory = "Dépenses journalières",
                Memo = "Faire des achats",
                Outflow = "€1 304,16",

                Payee = "Megacorp LLC",
            };

            var transactions = Program.GroupLineItems(
                new List<CSVLineItem>() { t1 },
                this.accountTypes,
                true,
                new CultureInfo("fr-FR"));
            string expected =
@"2019-01-01 * Megacorp LLC
 ; Faire des achats
 Assets:Compte Chèque  -1,304.16 EUR
 Expenses:Dépenses journalières:Supermarché  1,304.16 EUR
";

           string actual = Program.CreateLedger(transactions);
           Assert.That(actual, Is.EqualTo(expected));
        }

        /// <summary>
        /// Tests the behavior of the MemoWithoutSplit field to ensure it handles all 3 cases appropriately
        /// </summary>
        [Test]
        public void RarerCurrencySupported()
        {
            CSVLineItem t1 = new CSVLineItem()
            {
                Account = "Checking Account",

                Category = "Monthly Bills:Phone",
                SubCategory = "Phone",
                Date = new DateTime(2019, 1, 1),
                Inflow = "0,00ден.", // YNAB for some reason puts a period after the currency
                Cleared = "C",
                MasterCategory = "Monthly Bills",
                Memo = "Confirmation #: 1234",
                Outflow = "12,48ден.",
                Payee = "Megacorp LLC",
            };

            var transactions = Program.GroupLineItems(
                new List<CSVLineItem>() { t1 },
                this.accountTypes,
                true,
                new CultureInfo("mk"));

            // from what I can tell for Macedonian
            // positive amoungs have the currency symbol at the end, but negative amounts are in the beginning
            // see cultureInfo.NumberFormat.CurrencyPositivePattern/CurrencyNegativePattern
            string expected =
@"2019-01-01 * Megacorp LLC
 ; Confirmation #: 1234
 Assets:Checking Account  -12.48 MKD
 Expenses:Monthly Bills:Phone  12.48 MKD
";
            string actual = Program.CreateLedger(transactions);
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}