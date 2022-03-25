using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransactionExtractorProject
{
   public class ContractType
    {
        public decimal ContractSize { get; set; }
        public bool IsErrorInParsing { get; set; }
        public string ErrorMessage { get; set; }
    }
}
