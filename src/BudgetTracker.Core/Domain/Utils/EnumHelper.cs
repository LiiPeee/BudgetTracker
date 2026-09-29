using BudgetTracker.Core.Domain.Enum;


namespace BudgetTracker.Application
{
    public static class EnumHelper
    {
        public static int GetTypeContact(string enumValue)
        {
            return enumValue switch
            {
                "PERSONAL" => 1,
                "BUSINESS" => 2,
                _ => throw new NotImplementedException(),
            };
        }
        public static int GetTypeTransaction(string value)
        {
            return value switch
            {
                "EXPENSE" => 1,
                "INCOME" => 2,
                _ => throw new NotImplementedException(),
            };
        }
    }
}


