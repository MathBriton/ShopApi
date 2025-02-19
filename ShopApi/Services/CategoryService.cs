using Microsoft.Extensions.Caching.Memory;
using Supermarket.API.Domain.Repositories;
using Supermarket.API.Domain.Services;
using Supermarket.API.Domain.Services.Communication;
using Supermarket.API.Infrastructure;

namespace Supermarket.API.Services
{
	public class CategoryService : ICategoryService
	{
		private readonly ICategoryRepository _categoryRepository;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IMemoryCache _cache;
		private readonly ILogger<CategoryService> _logger;

		public CategoryService
		(
			ICategoryRepository categoryRepository,
			IUnitOfWork unitOfWork,
			IMemoryCache cache,
			ILogger<CategoryService> logger
		)
		{
			_categoryRepository = categoryRepository;
			_unitOfWork = unitOfWork;
			_cache = cache;
			_logger = logger;
		}

		public async Task<IEnumerable<Category>> ListAsync()
		{
			// Here I try to get the categories list from the memory cache. If there is no data in cache, the anonymous method will be
			// called, setting the cache to expire one minute ahead and returning the Task that lists the categories from the repository.
			var categories = await _cache.GetOrCreateAsync(CacheKeys.CategoriesList, (entry) =>
			{
				entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
				return _categoryRepository.ListAsync();
			});

			return categories ?? new List<Category>();
		}

		public async Task<Response<Category>> SaveAsync(Category category)
		{
			try
			{
				await _categoryRepository.AddAsync(category);
				await _unitOfWork.CompleteAsync();

				return new Response<Category>(category);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Não foi possível salvar.");
				return new Response<Category>($"Um erro aconteceu ao salvar: {ex.Message}");
			}
		}

		public async Task<Response<Category>> UpdateAsync(int id, Category category)
		{
			var existingCategory = await _categoryRepository.FindByIdAsync(id);
			if (existingCategory == null)
			{
				return new Response<Category>("Categoria não encontrada");
			}

			existingCategory.Name = category.Name;

			try
			{
				await _unitOfWork.CompleteAsync();
				return new Response<Category>(existingCategory);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Não foi possível atualizar pelo ID {id}.", id);
				return new Response<Category>($"Um erro aconteceu ao atualizar: {ex.Message}");
			}
		}

		public async Task<Response<Category>> DeleteAsync(int id)
		{
			var existingCategory = await _categoryRepository.FindByIdAsync(id);
			if (existingCategory == null)
			{
				return new Response<Category>("Categoria não encontrada.");
			}

			try
			{
				_categoryRepository.Remove(existingCategory);
				await _unitOfWork.CompleteAsync();

				return new Response<Category>(existingCategory);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Não foi possível deletar pela ID {id}.", id);
				return new Response<Category>($"Um erro aconteceu ao deletar pelo ID: {ex.Message}");
			}
		}
	}
}