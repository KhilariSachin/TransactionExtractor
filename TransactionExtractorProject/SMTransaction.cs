using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransactionExtractorProject
{
    public class SMTransaction
    {
        public string ISIN { get; set; }
        public string CFICode { get; set; }
        public string Venue { get; set; }
        public string AlgoParams { get; set; }
        public ContractType ContractType { get; set; }
    }
}
