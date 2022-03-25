using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyCsvParser.TypeConverter;

namespace TransactionExtractorProject
{
    class SMTransactionContractSizeTypeConverter : ITypeConverter<ContractType>
    {
        private readonly string KeyPlaceHolderHolder = "PriceMultiplier";

        public Type TargetType => typeof(ContractType);

        public bool TryConvert(string value, out ContractType result)
        {
            try
            {

                var resultValue = (value.Replace("|", string.Empty).Split(';')
                   .Where(x => x.ToUpper().Contains(KeyPlaceHolderHolder.ToUpper()))
                   .Select(x => x.Split(':'))).FirstOrDefault()[1];

                result = new ContractType()
                {
                    IsErrorInParsing = false,
                    ContractSize = Convert.ToDecimal(resultValue)
                };

                return true;

            }
            catch (Exception ex)
            {
                result = new ContractType()
                {
                    IsErrorInParsing = true,
                    ContractSize = 0,
                    ErrorMessage = ex.Message
                };
                return false;
            }

        }
    }
}
