using BudgetTracker.Core.Domain.Models.Request.Category;
using BudgetTracker.Application.Service;
using BudgetTracker.Core.Domain.Entities;
using BudgetTracker.Core.Domain.Repository;
using BudgetTracker.Core.Domain.UnitOfWork;
using Moq;

namespace Test;

public class CategoryServiceTest
{
    private Mock<ICategoryRepository> _categoryRepo;
    private Mock<IUnitOfWork> _unitOfWork;
    private CategoryAppService _service;

    [SetUp]
    public void Setup()
    {
        _categoryRepo = new Mock<ICategoryRepository>();
        _unitOfWork   = new Mock<IUnitOfWork>();
        _service      = new CategoryAppService(_categoryRepo.Object, _unitOfWork.Object);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task CreateAsync_ValidRequest_CallsAddAndCommit()
    {
        var request = new CategoryRequest { Name = "Food", Description = "Groceries" };
        _categoryRepo.Setup(r => r.AddAsync(It.IsAny<Category>())).ReturnsAsync(new Category { Id = 1, Name = "Food" });

        await _service.CreateAsync(request);

        _categoryRepo.Verify(r => r.AddAsync(It.Is<Category>(c =>
            c.Name == "Food" &&
            c.Description == "Groceries" &&
            c.IsActive == true)), Times.Once);

        _unitOfWork.Verify(u => u.BeginTransaction(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Once);
    }

    [Test]
    public async Task CreateAsync_RepositoryThrows_RollsBackAndRethrows()
    {
        var request = new CategoryRequest { Name = "Food", Description = "Groceries" };
        _categoryRepo.Setup(r => r.AddAsync(It.IsAny<Category>())).ThrowsAsync(new Exception("db error"));

        Assert.ThrowsAsync<Exception>(() => _service.CreateAsync(request));

        _unitOfWork.Verify(u => u.Rollback(), Times.Once);
        _unitOfWork.Verify(u => u.Commit(), Times.Never);
    }

    [Test]
    public async Task CreateAsync_SetsIsActiveTrue()
    {
        Category? saved = null;
        _categoryRepo
            .Setup(r => r.AddAsync(It.IsAny<Category>()))
            .Callback<Category>(c => saved = c)
            .ReturnsAsync(new Category { Id = 1 });

        await _service.CreateAsync(new CategoryRequest { Name = "Transport", Description = null });

        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.IsActive, Is.True);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task GetAllAsync_ReturnsAllMappedCategories()
    {
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Food",      Description = "desc1" },
            new() { Id = 2, Name = "Transport", Description = "desc2" },
        };

        _categoryRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(categories);

        var result = (await _service.GetAllAsync()).ToList();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Food"));
        Assert.That(result[1].Id,   Is.EqualTo(2));
    }

    [Test]
    public async Task GetAllAsync_EmptyRepository_ReturnsEmptyList()
    {
        _categoryRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Category>());

        var result = await _service.GetAllAsync();

        Assert.That(result, Is.Empty);
    }
}
