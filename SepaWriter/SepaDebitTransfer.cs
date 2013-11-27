﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using SepaWriter.Utils;

namespace SepaWriter
{
    public enum LocalInstrumentCode
    {
        CORE,
        B2B
    }
    /// <summary>
    ///     Manage SEPA (Single Euro Payments Area) DebitTransfer for SEPA or international order.
    ///     Only one PaymentInformation is managed but it can manage multiple transactions.
    /// </summary>
    public class SepaDebitTransfer
    {
        private decimal headerControlSum;
        private decimal paymentControlSum;
        private SepaIbanData debtor;
        private List<SepaDebitTransferTransaction> transactions = new List<SepaDebitTransferTransaction>();

        /// <summary>
        ///     Number of payment transactions.
        /// </summary>
        private int numberOfTransactions;

        /// <summary>
        ///     Purpose of the transaction(s)
        /// </summary>
        public string CategoryPurposeCode { get; set; }

        /// <summary>
        ///     Creation Date (default is object creation date)
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        ///     Debtor account ISO currency code (default is EUR)
        /// </summary>
        public string DebtorAccountCurrency { get; set; }

        public string InitiatingPartyId { get; set; }

        public string InitiatingPartyName { get; set; }

        /// <summary>
        ///     Local service instrument code
        /// </summary>
        public LocalInstrumentCode LocalInstrumentCode { get; set; }

        /// <summary>
        ///     The Message identifier
        /// </summary>
        public string MessageIdentification { get; set; }

        /// <summary>
        ///     The single Payment information identifier (uses Message identifier if not defined)
        /// </summary>
        public string PaymentInfoId { get; set; }

        /// <summary>
        ///     Unique and unambiguous identification of a person. SEPA creditor
        /// </summary>
        public string PersonId { get; set; }

        /// <summary>
        ///     Payment method
        /// </summary>
        private string paymentMethod = Constant.DebitTransfertPaymentMethod;

        /// <summary>
        ///     Requested Execution Date (default is object creation date)
        /// </summary>
        public DateTime RequestedExecutionDate { get; set; }

        public SepaDebitTransfer()
        {
            CreationDate = DateTime.Now;
            RequestedExecutionDate = CreationDate.Date;
            DebtorAccountCurrency = Constant.EuroCurrency;
        }

        /// <summary>
        ///     Debtor IBAN data
        /// </summary>
        /// <exception cref="SepaRuleException">If debtor to set is not valid.</exception>
        public SepaIbanData Debtor
        {
            get { return debtor; }
            set
            {
                if (!value.IsValid)
                    throw new SepaRuleException("Debtor IBAN data are invalid.");
                debtor = value;
            }
        }

        /// <summary>
        ///     Header control sum in cents.
        /// </summary>
        /// <returns></returns>
        public decimal HeaderControlSumInCents
        {
            get { return headerControlSum * 100; }
        }

        /// <summary>
        ///    Payment control sum in cents.
        /// </summary>
        /// <returns></returns>
        public decimal PaymentControlSumInCents
        {
            get { return paymentControlSum * 100; }
        }

        /// <summary>
        ///     Return the XML string
        /// </summary>
        /// <returns></returns>
        public string AsXmlString()
        {
            return GenerateXml().OuterXml;
        }

        /// <summary>
        ///     Save in an XML file
        /// </summary>
        public void Save(string filename)
        {
            GenerateXml().Save(filename);
        }

        /// <summary>
        ///     Add an existing Debit transfer transaction
        /// </summary>
        /// <param name="transfer"></param>
        /// <exception cref="ArgumentNullException">If transfert is null.</exception>
        public void AddDebitTransfer(SepaDebitTransferTransaction transfer)
        {
            if (transfer == null)
                throw new ArgumentNullException("transfer");

            transfer = (SepaDebitTransferTransaction)transfer.Clone();
            if (transfer.EndToEndId == null)
                transfer.EndToEndId = (PaymentInfoId ?? MessageIdentification) + "/" + (numberOfTransactions + 1);
            CheckTransactionIdUnicity(transfer.Id, transfer.EndToEndId);
            transactions.Add(transfer);
            numberOfTransactions++;
            headerControlSum += transfer.Amount;
            paymentControlSum += transfer.Amount;
        }

        /// <summary>
        ///     Check If the id is not defined in others transactions excepts null values
        /// </summary>
        /// <param name="id"></param>
        /// <param name="endToEndId"></param>
        /// <exception cref="SepaRuleException">If an id is already used.</exception>
        private void CheckTransactionIdUnicity(string id, string endToEndId)
        {
            if (id == null)
                return;

            if (transactions.Exists(transfert => transfert.Id != null && transfert.Id == id))
            {
                throw new SepaRuleException("Transaction Id '" + id + "' must be unique in a transfer.");
            }

            if (transactions.Exists(transfert => transfert.EndToEndId != null && transfert.EndToEndId == endToEndId))
            {
                throw new SepaRuleException("End to End Id '" + endToEndId + "' must be unique in a transfer.");
            }
        }

        /// <summary>
        ///     Is Mandatory data are set ? In other case a SepaRuleException will be thrown
        /// </summary>
        /// <exception cref="SepaRuleException">If mandatory data is missing.</exception>
        private void CheckMandatoryData()
        {
            if (transactions.Count == 0)
            {
                throw new SepaRuleException("At least one transaction is needed in a transfer.");
            }
            if (Debtor == null)
            {
                throw new SepaRuleException("The debtor is mandatory.");
            }
            if (string.IsNullOrEmpty(MessageIdentification))
            {
                throw new SepaRuleException("The message identification is mandatory.");
            }
            if (string.IsNullOrEmpty(InitiatingPartyName))
            {
                throw new SepaRuleException("The initial party name is mandatory.");
            }
        }

        /// <summary>
        ///     Generate the XML structure
        /// </summary>
        /// <returns></returns>
        protected XmlDocument GenerateXml()
        {
            CheckMandatoryData();

            var xml = new XmlDocument();
            xml.AppendChild(xml.CreateXmlDeclaration("1.0", Encoding.UTF8.BodyName, "yes"));
            var el = (XmlElement)xml.AppendChild(xml.CreateElement("Document"));
            el.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            el.SetAttribute("xmlns", "urn:iso:std:iso:20022:tech:xsd:pain.001.001.03");
            el.NewElement("CstmrDrctDbtInitn");

            // Part 1: Group Header
            var grpHdr = XmlUtils.GetFirstElement(xml, "CstmrDrctDbtInitn").NewElement("GrpHdr");
            grpHdr.NewElement("MsgId", MessageIdentification);
            grpHdr.NewElement("CreDtTm", StringUtils.FormatDateTime(CreationDate));
            grpHdr.NewElement("NbOfTxs", numberOfTransactions);
            grpHdr.NewElement("CtrlSum", StringUtils.FormatAmount(headerControlSum));
            grpHdr.NewElement("InitgPty").NewElement("Nm", InitiatingPartyName);
            if (InitiatingPartyId != null)
                grpHdr.NewElement("InitgPty").NewElement("Id", InitiatingPartyId);

            // Part 2: Payment Information
            var pmtInf = XmlUtils.GetFirstElement(xml, "CstmrDrctDbtInitn").NewElement("PmtInf");
            pmtInf.NewElement("PmtInfId", PaymentInfoId ?? MessageIdentification);
            if (CategoryPurposeCode != null)
                pmtInf.NewElement("CtgyPurp").NewElement("Cd", CategoryPurposeCode);

            pmtInf.NewElement("PmtMtd", paymentMethod);
            pmtInf.NewElement("NbOfTxs", numberOfTransactions);
            pmtInf.NewElement("CtrlSum", StringUtils.FormatAmount(paymentControlSum));
            pmtInf.NewElement("PmtTpInf").NewElement("SvcLvl").NewElement("Cd", "SEPA");
            if (LocalInstrumentCode != null)
                XmlUtils.GetFirstElement(xml, "PmtTpInf").NewElement("LclInstrm")
                        .NewElement("Cd", LocalInstrumentCode.ToString());

            pmtInf.NewElement("ReqdColltnDt", StringUtils.FormatDate(RequestedExecutionDate));
            pmtInf.NewElement("Cdtr").NewElement("Nm", Debtor.Name);

            var dbtrAcct = pmtInf.NewElement("CdtrAcct");
            dbtrAcct.NewElement("Id").NewElement("IBAN", Debtor.Iban);
            dbtrAcct.NewElement("Ccy", DebtorAccountCurrency);

            pmtInf.NewElement("CdtrAgt").NewElement("FinInstnId").NewElement("BIC", Debtor.Bic);
            pmtInf.NewElement("ChrgBr", "SLEV");

            var othr = pmtInf.NewElement("CdtrSchmeId").NewElement("Id")
                    .NewElement("PrvtId")
                        .NewElement("Othr");
            othr.NewElement("Id", PersonId);
            othr.NewElement("SchmeNm").NewElement("Prtry", "SEPA");
            // Part 3: Debit Transfer Transaction Information
            foreach (SepaDebitTransferTransaction transfer in transactions)
            {
                GenerateTransaction(pmtInf, transfer);
            }

            return xml;
        }

        /// <summary>
        /// Generate the Transaction XML part
        /// </summary>
        /// <param name="pmtInf">The root nodes for a transaction</param>
        /// <param name="transfer">The transaction to generate</param>
        private static void GenerateTransaction(XmlElement pmtInf, SepaDebitTransferTransaction transfer)
        {
            var cdtTrfTxInf = pmtInf.NewElement("DrctDbtTxInf");
            var pmtId = cdtTrfTxInf.NewElement("PmtId");
            if (transfer.Id != null)
                pmtId.NewElement("InstrId", transfer.Id);
            pmtId.NewElement("EndToEndId", transfer.EndToEndId);
            cdtTrfTxInf.NewElement("InstdAmt", StringUtils.FormatAmount(transfer.Amount)).SetAttribute("Ccy", transfer.Currency);
           
            var MndtRltdInf = cdtTrfTxInf.NewElement("DrctDbtTx").NewElement("MndtRltdInf");
            MndtRltdInf.NewElement("MndtId", transfer.MandateIdentification);
            MndtRltdInf.NewElement("DtOfSgntr", transfer.DateOfSignature);

            cdtTrfTxInf.NewElement("DbtrAgt").NewElement("FinInstnId").NewElement("BIC", transfer.Creditor.Bic);
            cdtTrfTxInf.NewElement("Dbtr").NewElement("Nm", transfer.Creditor.Name);
            cdtTrfTxInf.NewElement("DbtrAcct").NewElement("Id").NewElement("IBAN", transfer.Creditor.Iban);
            cdtTrfTxInf.NewElement("RmtInf").NewElement("Ustrd", transfer.RemittanceInformation);
        }
    }
}