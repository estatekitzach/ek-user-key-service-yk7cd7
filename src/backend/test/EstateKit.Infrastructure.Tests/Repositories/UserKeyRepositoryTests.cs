using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EstateKit.Infrastructure.Data.Repositories;
using EstateKit.Core.Entities;
using EstateKit.Core.Constants;

namespace EstateKit.Infrastructure.Tests.Repositories
{
    /// <summary>
    /// Comprehensive test suite for UserKeyRepository class validating key management operations,
    /// security controls, and concurrent access handling.
    /// </summary>
    [Collection("Sequential")]
    public class UserKeyRepositoryTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UserKeyRepository _repository;
        private readonly ITestOutputHelper _output;
        private readonly ILogger<UserKeyRepository> _logger;

        public UserKeyRepositoryTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Set up in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"UserKeyDb_{Guid.NewGuid()}")
                .Options;
            
            _context = new ApplicationDbContext(options);
            
            // Set up logger mock
            var loggerMock = new Mock<ILogger<UserKeyRepository>>();
            _logger = loggerMock.Object;
            
            _repository = new UserKeyRepository(_context, _logger);
        }

        [Fact]
        public async Task GetActiveKeyAsync_ExistingKey_ReturnsKey()
        {
            // Arrange
            const long userId = 1;
            var testKey = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Test+Key");
            await _context.UserKeys.AddAsync(testKey);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveKeyAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(userId);
            result.IsActive.Should().BeTrue();
            result.Key.Should().Be(testKey.Key);
            UserKey.ValidateKey(result.Key).Should().BeTrue();
        }

        [Fact]
        public async Task GetActiveKeyAsync_ExpiredKey_ReturnsNull()
        {
            // Arrange
            const long userId = 1;
            var testKey = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Test+Key");
            
            // Set creation date beyond 90-day expiration
            var createdAtField = typeof(UserKey).GetField("CreatedAt", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            createdAtField?.SetValue(testKey, DateTime.UtcNow.AddDays(-91));
            
            await _context.UserKeys.AddAsync(testKey);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveKeyAsync(userId);

            // Assert
            result.Should().BeNull();
            (await _context.UserKeys.SingleAsync()).IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task CreateKeyAsync_ValidKey_CreatesAndReturnsKey()
        {
            // Arrange
            const long userId = 1;
            var newKey = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2New+Key");

            // Act
            var result = await _repository.CreateKeyAsync(newKey);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(userId);
            result.IsActive.Should().BeTrue();
            result.RotationVersion.Should().Be(1);
            UserKey.ValidateKey(result.Key).Should().BeTrue();
            
            // Verify database state
            var savedKey = await _context.UserKeys.SingleAsync();
            savedKey.Should().NotBeNull();
            savedKey.Key.Should().Be(newKey.Key);
        }

        [Fact]
        public async Task CreateKeyAsync_ExistingActiveKey_DeactivatesOldKey()
        {
            // Arrange
            const long userId = 1;
            var oldKey = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Old+Key");
            await _context.UserKeys.AddAsync(oldKey);
            await _context.SaveChangesAsync();

            var newKey = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2New+Key");

            // Act
            var result = await _repository.CreateKeyAsync(newKey);

            // Assert
            var keys = await _context.UserKeys.ToListAsync();
            keys.Count.Should().Be(2);
            keys.Count(k => k.IsActive).Should().Be(1);
            keys.Single(k => k.IsActive).Key.Should().Be(newKey.Key);
        }

        [Fact]
        public async Task UpdateKeyAsync_ConcurrentAccess_HandlesCorrectly()
        {
            // Arrange
            const long userId = 1;
            var originalKey = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Original+Key");
            await _context.UserKeys.AddAsync(originalKey);
            await _context.SaveChangesAsync();

            var updateKey1 = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Update1+Key");
            var updateKey2 = new UserKey(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Update2+Key");

            // Act & Assert
            await _repository.UpdateKeyAsync(updateKey1);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => 
                _repository.UpdateKeyAsync(updateKey2));
        }

        [Fact]
        public async Task AddKeyHistoryAsync_SecurityAudit_ValidatesCompletely()
        {
            // Arrange
            const long userId = 1;
            var keyHistory = new UserKeyHistory(
                userId,
                "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2History+Key",
                "Scheduled Rotation",
                "System",
                "1.0.0"
            );

            // Act
            var result = await _repository.AddKeyHistoryAsync(keyHistory);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(userId);
            result.RotationDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.RotationReason.Should().Be("Scheduled Rotation");
            
            // Verify database state
            var savedHistory = await _context.UserKeyHistory.SingleAsync();
            savedHistory.Should().NotBeNull();
            savedHistory.KeyValue.Should().Be(keyHistory.KeyValue);
            savedHistory.CreatedBy.Should().Be("System");
            savedHistory.SystemVersion.Should().Be("1.0.0");
        }

        [Fact]
        public async Task GetKeyHistoryAsync_MultipleRecords_ReturnsOrderedHistory()
        {
            // Arrange
            const long userId = 1;
            var histories = new List<UserKeyHistory>
            {
                new UserKeyHistory(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2First+Key", 
                    "Initial Creation", "System", "1.0.0"),
                new UserKeyHistory(userId, "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2Second+Key",
                    "Scheduled Rotation", "System", "1.0.0")
            };

            await _context.UserKeyHistory.AddRangeAsync(histories);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetKeyHistoryAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.Count().Should().Be(2);
            result.Should().BeInDescendingOrder(h => h.RotationDate);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}