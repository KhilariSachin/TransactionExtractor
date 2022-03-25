using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TransactionExtractorProject;

namespace TransactionExtractorTestProject
{
    public class SMTransactionParserTests
    {

        [Test]
        public void ValidateRowCount()
        {
            string inputFilePath = @"C:\Users\Sachin\Desktop\Files\DataExtractor_Example_Input.csv";
            SMTransactionParser.ReadAndParseInputFIle(inputFilePath);


            Assert.AreEqual(2, SMTransactionParser.SMTransactionList.Count());

            Assert.Pass();
        }

        [Test]
        public void ValidateInputFile()
        {
            string inputFilePath = @"C:\Users\Sachin\Desktop\Files\DataExtractor_Example_Input.csv";
            SMTransactionParser.ReadAndParseInputFIle(inputFilePath);

            Assert.IsTrue(SMTransactionParser.SMTransactionList != null && SMTransactionParser.SMTransactionList.Any(x => !x.ContractType.IsErrorInParsing));
            Assert.Pass();
        }

        [Test]
        public void ValidateFileContent()
        {
            string inputFilePath = @"C:\Users\Sachin\Desktop\Files\DataExtractor_Example_Input.csv";
            SMTransactionParser.ReadAndParseInputFIle(inputFilePath);

            Assert.IsTrue(SMTransactionParser.SMTransactionList != null);

            string ISIN = "DE000ABCDEFG";
            string CFICode = "FFICSX";
            string Venue = "XEUR";
            decimal ContractSize = 20;

            var data = SMTransactionParser.SMTransactionList.ToList();
            Assert.AreEqual(ISIN, data.Where(x => x.ISIN == ISIN).Select(x => x.ISIN).FirstOrDefault());
            Assert.AreEqual(CFICode, data.Where(x => x.CFICode == CFICode).Select(x => x.CFICode).FirstOrDefault());
            Assert.AreEqual(Venue, data.Where(x => x.Venue == Venue).Select(x => x.Venue).FirstOrDefault());
            Assert.AreEqual(ContractSize, data.Where(x => x.ContractType.ContractSize == ContractSize).Select(x => x.ContractType.ContractSize).FirstOrDefault());

            Assert.Pass();
        }

        [Test]
        public void IsOutputFileGeneratedTest()
        {
            string inputFilePath = @"C:\Users\Sachin\Desktop\Files\DataExtractor_Example_Input.csv";
            string outputFilePath = @"C:\Users\Sachin\Desktop\Files\Ouput.csv";
            SMTransactionParser.ReadAndParseInputFIle(inputFilePath);
            SMTransactionParser.GenerateOutputFile(outputFilePath);

            Assert.IsTrue(File.Exists(outputFilePath));

            Assert.Pass();
        }


    }
}