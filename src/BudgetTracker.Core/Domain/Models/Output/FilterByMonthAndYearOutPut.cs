using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetTracker.Core.Domain.Dtos.Output
{
    public class FilterByMonthAndYearOutPut
    {
        public long Id { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public bool Paid { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? Name { get; set; }
        public long? TypeTransaction { get; set; }
        public CategoryOutput? Category { get; set; }
        public ContactOutput? Contact { get; set; }
        public string? QuantityOfInstallment { get; set; }
        public DateTime? DateOfInstallment { get; set; }
        public DateTime? CompetenceDate { get; set; }
        public long? Recurrence { get; set; }
        public string? SubCategory { get; set; }
    }

    public class ContactOutput
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Document { get; set; }
        public long TypeContactId { get; set; }
        public bool IsActive { get; set; }
    }
    public class CategoryOutput
    {
        public string? Name { get; set; }
    }
}


