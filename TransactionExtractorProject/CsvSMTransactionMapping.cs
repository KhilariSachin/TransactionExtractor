using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyCsvParser.Mapping;

namespace TransactionExtractorProject
{
    class CsvSMTransactionMapping : CsvMapping<SMTransaction>
    {
        public CsvSMTransactionMapping():base()
        {
            MapProperty(1, x => x.ISIN);
            MapProperty(3, x => x.Venue);
            MapProperty(6, x => x.CFICode);
            MapProperty(35, x => x.ContractType, new SMTransactionContractSizeTypeConverter());

        }
    }
}
