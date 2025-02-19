using Microsoft.Extensions.Caching.Memory;
using Supermarket.API.Domain.Repositories;
using Supermarket.API.Domain.Services;
using Supermarket.API.Domain.Services.Communication;
using Supermarket.API.Infrastructure;

namespace Supermarket.API.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ProductService> _logger;

        public ProductService
        (
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            ILogger<ProductService> logger
        )
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
            _cache = cache;
            _logger = logger;
        }

        public async Task<QueryResult<Product>> ListAsync(ProductsQuery query)
        {
            // Here I list the query result from cache if they exist, but now the data can vary according to the category ID, page and amount of
            // items per page. I have to compose a cache to avoid returning wrong data.
            string cacheKey = GetCacheKeyForProductsQuery(query);

            var products = await _cache.GetOrCreateAsync(cacheKey, (entry) =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return _productRepository.ListAsync(query);
            });

            return products!;
        }

        public async Task<Response<Product>> SaveAsync(Product product)
        {
            try
            {
                /*
                 Notice here we have to check if the category ID is valid before adding the product, to avoid errors.
                 You can create a method into the CategoryService class to return the category and inject the service here if you prefer, but 
                 it doesn't matter given the API scope.
                */
                var existingCategory = await _categoryRepository.FindByIdAsync(product.CategoryId);
                if (existingCategory == null)
                    return new Response<Product>("Categoria inválida.");

                await _productRepository.AddAsync(product);
                await _unitOfWork.CompleteAsync();

                return new Response<Product>(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Não foi possível salvar.");
                return new Response<Product>($"Um erro ocorreu ao salvar: {ex.Message}");
            }
        }

        public async Task<Response<Product>> UpdateAsync(int id, Product product)
        {
            var existingProduct = await _productRepository.FindByIdAsync(id);

            if (existingProduct == null)
                return new Response<Product>("Produto não encontrado.");

            var existingCategory = await _categoryRepository.FindByIdAsync(product.CategoryId);
            if (existingCategory == null)
                return new Response<Product>("Categoria inválida.");

            existingProduct.Name = product.Name;
            existingProduct.UnitOfMeasurement = product.UnitOfMeasurement;
            existingProduct.QuantityInPackage = product.QuantityInPackage;
            existingProduct.CategoryId = product.CategoryId;

            try
            {
                _productRepository.Update(existingProduct);
                await _unitOfWork.CompleteAsync();

                return new Response<Product>(existingProduct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Não foi possível atualizar o produto {id}.", id);
                return new Response<Product>($"Um erro aconteceu ao atualizar o produto: {ex.Message}");
            }
        }

        public async Task<Response<Product>> DeleteAsync(int id)
        {
            var existingProduct = await _productRepository.FindByIdAsync(id);

            if (existingProduct == null)
                return new Response<Product>("Product not found.");

            try
            {
                _productRepository.Remove(existingProduct);
                await _unitOfWork.CompleteAsync();

                return new Response<Product>(existingProduct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Não foi possível deletar pelo ID {id}.", id);
                return new Response<Product>($"Um erro aconteceu ao deletar o produto: {ex.Message}");
            }
        }

        private static string GetCacheKeyForProductsQuery(ProductsQuery query)
            => $"{CacheKeys.ProductsList}_{query.CategoryId}_{query.Page}_{query.ItemsPerPage}";
    }
}