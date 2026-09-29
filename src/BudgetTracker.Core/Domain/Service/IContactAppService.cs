using BudgetTracker.Application.Dtos.Request;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Models.Request.Contact;

namespace BudgetTracker.Core.Domain.Service;

public interface IContactAppService
{
    Task<ContactOutput?> CreateAsync(long accountId, CreateContactRequest request);
    Task<List<ContactOutput?>> GetAllsync(long accountId);
    Task EditContactAsync(long accountId, ContactRequest request);
    Task DeleteContactAsync(long accountId, string contactId);
}


