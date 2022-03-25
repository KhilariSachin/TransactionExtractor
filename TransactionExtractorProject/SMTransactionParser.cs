using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TinyCsvParser;

namespace TransactionExtractorProject
{
    public static class SMTransactionParser
    {
        #region DataMember
        
        private static IEnumerable<SMTransaction> _SMTransactionList;

        #endregion

        #region Properties
        //This property will hold data of all read and needed column record of input CSV file
        public static IEnumerable<SMTransaction> SMTransactionList
        {
            get
            {
                return _SMTransactionList;
            }
        }
        #endregion


        static void Main(string[] args)
        {
            string inputFilePath = @"C:\Users\Sachin\Desktop\Files\DataExtractor_Example_Input.csv";
            string outputFilePath = @"C:\Users\Sachin\Desktop\Files\Ouput.csv";
            ReadAndParseInputFIle(inputFilePath);
            GenerateOutputFile(outputFilePath);


            Console.ReadLine();
        }


        public static void ReadAndParseInputFIle(string inputFilePath)
        {
            CsvParserOptions csvParserOptions = new CsvParserOptions(true, ',');

            CsvSMTransactionMapping csvSMTransactionMapping = new CsvSMTransactionMapping();

            var csvParser = new CsvParser<SMTransaction>(csvParserOptions, csvSMTransactionMapping);

            var fileData = csvParser.ReadFromFile(inputFilePath, Encoding.UTF8);

            _SMTransactionList = fileData.Where(x => x.RowIndex > 1).Select(x => x.Result);
        }

        public static void GenerateOutputFile(string outputFilePath)
        {
            using (var file = File.CreateText(outputFilePath))
            {
                file.WriteLine(string.Join(",", "ISIN", "CFICode", "Venue", "Contract Size"));

                foreach (var arr in _SMTransactionList.ToList())
                {
                    file.WriteLine(string.Join(",", arr.ISIN, arr.CFICode, arr.Venue, arr.ContractType.ContractSize));
                }
            }

        }
    }
}
