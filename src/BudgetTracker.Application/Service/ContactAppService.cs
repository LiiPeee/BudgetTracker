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

            // Transações apontam para o contato pelo nome, então ele não pode se repetir.
            if (await _contactRepository.GetByNameAsync(accountId, request.Name) is not null)
                throw new ArgumentException("contact with this name already exists");

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
            var contacts = await _contactRepository.GetByIdAccount(accountId);
            var addresses = (await _addressRepository.GetByAccountAsync(accountId)).ToLookup(a => a.ContactId);

            return contacts.Select(contact =>
            {
                var output = ToOutput(contact!);
                var address = addresses[output.Id].FirstOrDefault();
                if (address is not null)
                {
                    output.Street = address.Street;
                    output.City = address.City;
                    output.State = address.State;
                    output.ZipCode = address.ZipCode;
                    output.Country = address.Country;
                    output.IsPrimary = address.IsPrimary;
                }
                return output;
            }).ToList<ContactOutput?>();
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

            var contact = await _contactRepository.GetByIdAsync(id, accountId);

            if (contact is null)
                throw new UnauthorizedAccessException("Contact not found or access denied");

            // Contato excluído não volta por uma edição.
            if (!contact.IsActive)
                throw new KeyNotFoundException("Contact not found or access denied");

            if (contact.Name != request.Name
                && await _contactRepository.GetByNameAsync(accountId, request.Name) is { } other
                && other.Id != id)
                throw new ArgumentException("contact with this name already exists");

            // Altera o registro lido, para não apagar CreatedAt nem IsActive.
            contact.Document = request.Document;
            contact.Email = request.Email;
            contact.Name = request.Name;
            contact.Phone = request.Phone;
            contact.TypeContactId = EnumHelper.GetTypeContact(request.TypeContact.ToString());
            contact.UpdatedAt = DateTime.UtcNow;

            var address = (await _addressRepository.GetByAccountAsync(accountId)).FirstOrDefault(a => a.ContactId == id);

            _unitOfWork.BeginTransaction();
            await _contactRepository.UpdateAsync(contact);
            await SaveAddressAsync(accountId, id, address, request);
            _unitOfWork.Commit();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }

    private async Task SaveAddressAsync(long accountId, long contactId, Address? address, ContactRequest request)
    {
        var isNew = address is null;
        address ??= new Address { AccountId = accountId, ContactId = contactId, CreatedAt = DateTime.UtcNow };

        address.Street = request.Street;
        address.City = request.City;
        address.State = request.State;
        address.ZipCode = request.ZipCode;
        address.Country = request.Country;
        address.IsPrimary = request.IsPrimary;

        if (isNew)
        {
            await _addressRepository.AddAsync(address);
            return;
        }

        address.UpdatedAt = DateTime.UtcNow;
        await _addressRepository.UpdateAsync(address);
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


