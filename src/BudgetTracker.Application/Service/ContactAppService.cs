using BudgetTracker.Application.Dtos.Request;
using BudgetTracker.Core.Domain.Dtos.Output;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Models.Request.Contact;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.Service;
using BudgetTracker.Core.Domain.UnitOfWork;

namespace BudgetTracker.Application.Service;

public class ContactAppService : IContactAppService
{
    private readonly IContactRepository _contactRepository;
    private readonly IAddressRepository _addressRepository;
    public readonly IUnitOfWork _unitOfWork;

    public ContactAppService(IContactRepository contactRepository, IAddressRepository addressRepository, IUnitOfWork unitOfWork)
    {
        _contactRepository = contactRepository;
        _addressRepository = addressRepository;
        _unitOfWork = unitOfWork;
    }

    private static ContactOutput ToOutput(Contact contact) => new()
    {
        Id = contact.Id,
        Name = contact.Name,
        Email = contact.Email,
        Phone = contact.Phone,
        Document = contact.Document,
        TypeContactId = contact.TypeContactId,
        IsActive = contact.IsActive,
    };

    public async Task<ContactOutput?> CreateAsync(long accountId, CreateContactRequest request)
    {
        try
        {
            var typeContactId = EnumHelper.GetTypeContact(request.TypeContact.ToString());

            Contact contact = new Contact()
            {
                CreatedAt = DateTime.UtcNow,
                Document = request.Document,
                Email = request.Email,
                IsActive = true,
                Name = request.Name,
                Phone = request.Phone,
                AccountId = accountId,
                TypeContactId = typeContactId,
            };

            _unitOfWork.BeginTransaction();

            var savedContact = await _contactRepository.AddAsync(contact);

            Address address = new()
            {
                City = request.City,
                Country = request.Country,
                CreatedAt = DateTime.UtcNow,
                IsPrimary = request.IsPrimary,
                State = request.State,
                Street = request.Street,
                ZipCode = request.ZipCode,
                ContactId = savedContact.Id,
                AccountId = accountId,
            };

            await _addressRepository.AddAsync(address);

            _unitOfWork.Commit();

            return ToOutput(savedContact);
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task<List<ContactOutput?>> GetAllsync(long accountId)
    {
        try
        {
            var contact = await _contactRepository.GetByIdAccount(accountId);
            return contact.Select(ToOutput).ToList<ContactOutput?>();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task EditContactAsync(long accountId, ContactRequest request)
    {
        try
        {
            if (!long.TryParse(request.ContactId, out var id))
                throw new ArgumentException("ContactId inválido");

            var existingContact = await _contactRepository.GetByIdAsync(id, accountId);

            if (existingContact is null)
                throw new UnauthorizedAccessException("Contact not found or access denied");

            Contact contact = new Contact()
            {
                Id = id,
                AccountId = accountId,
                Document = request.Document,
                Email = request.Email,
                Name = request.Name,
                Phone = request.Phone,
                TypeContactId = EnumHelper.GetTypeContact(request.TypeContact.ToString()),
                UpdatedAt = DateTime.UtcNow,
            };

            _unitOfWork.BeginTransaction();
            await _contactRepository.UpdateAsync(contact);
            _unitOfWork.Commit();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    public async Task DeleteContactAsync(long accountId, string contactId)
    {
        try
        {
            if (!long.TryParse(contactId, out var id))
                throw new ArgumentException("ContactId inválido");

            var contact = await _contactRepository.GetByIdAsync(id, accountId);
            if (contact is null)
                throw new UnauthorizedAccessException("Contact not found or access denied");

            contact.IsActive = false;

            _unitOfWork.BeginTransaction();
            await _contactRepository.UpdateAsync(contact);
            _unitOfWork.Commit();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }
}


