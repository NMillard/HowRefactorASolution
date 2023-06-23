using Microsoft.Extensions.DependencyInjection;

namespace BusinessRules.Demo.DynamicBusinessRules;

public class Demo
{
    [Theory]
    [MemberData(nameof(Usernames))]
    public void First(string value, bool expected)
    {
        var repository = new InMemoryUsernameConfigurationRepository();
        var usernamePolicies = new List<IUsernamePolicy>
        {
            new MaxLengthPolicy(repository),
            new MinLengthPolicy(repository),
            new OnlyAlphanumericCharacters()
        };

        var sut = new UsernameEvaluator(usernamePolicies, repository);

        bool result = sut.Evaluate(new Username(value));

        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(Usernames))]
    public void Second(string value, bool expected)
    {
        ServiceProvider services = new ServiceCollection()
            .AddScoped<IUsernamePolicy, MaxLengthPolicy>()
            .AddScoped<IUsernamePolicy, MinLengthPolicy>()
            .AddScoped<IUsernamePolicy, OnlyAlphanumericCharacters>()
            .AddScoped<UsernameEvaluator>()
            .AddScoped<IUsernameConfigurationRepository, InMemoryUsernameConfigurationRepository>()
            .BuildServiceProvider();

        var sut = services.GetRequiredService<UsernameEvaluator>();

        bool result = sut.Evaluate(new Username(value));

        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> Usernames()
    {
        return new List<object[]>
        {
            new object[] { "a", false },
            new object[] { "", false },
            new object[] { null!, false },
            new object[] { "abc", true },
            new object[] { "12345678901234567890", true },
            new object[] { "12345678901234567890", true },
            new object[] { "hello*", true },
        };
    }
}

public record Username(string Value);

public class UsernameEvaluator
{
    public UsernameEvaluator(
        IEnumerable<IUsernamePolicy> policies,
        IUsernameConfigurationRepository repository)
    {
        this.policies = policies;
        this.repository = repository;
    }

    private readonly IEnumerable<IUsernamePolicy> policies;
    private readonly IUsernameConfigurationRepository repository;

    public bool Evaluate(Username username)
    {
        if (string.IsNullOrEmpty(username.Value)) return false;

        List<UsernameConfiguration> configurations = repository.GetAll()
            .Where(uc => uc.IsActive)
            .ToList();

        List<IUsernamePolicy> activePolicies = policies
            .Where(p => configurations.Any(uc => uc.Key.Equals(p.GetType().Name) && uc.IsActive))
            .ToList();

        return activePolicies.All(p => p.Evaluate(username));
    }
}

public interface IUsernamePolicy
{
    bool Evaluate(Username username);
}

public class MaxLengthPolicy : IUsernamePolicy
{
    private readonly IUsernameConfigurationRepository repository;

    public MaxLengthPolicy(IUsernameConfigurationRepository repository)
    {
        this.repository = repository;
    }

    public bool Evaluate(Username username)
    {
        UsernameConfiguration? config = repository.GetByName(nameof(MaxLengthPolicy));
        int maxLength = config?.Value as int? ?? 50;

        return username.Value.Length <= maxLength;
    }
}

public class MinLengthPolicy : IUsernamePolicy
{
    private readonly IUsernameConfigurationRepository repository;

    public MinLengthPolicy(IUsernameConfigurationRepository repository)
    {
        this.repository = repository;
    }

    public bool Evaluate(Username username)
    {
        UsernameConfiguration? config = repository.GetByName(nameof(MinLengthPolicy));
        int minLength = config?.Value as int? ?? 3;

        return username.Value.Length >= minLength;
    }
}

public class OnlyAlphanumericCharacters : IUsernamePolicy
{
    public bool Evaluate(Username username)
    {
        return username.Value.All(char.IsLetterOrDigit);
    }
}

public interface IUsernameConfigurationRepository
{
    UsernameConfiguration? GetByName(string policyName);
    IEnumerable<UsernameConfiguration> GetAll();
}

public class InMemoryUsernameConfigurationRepository : IUsernameConfigurationRepository
{
    private List<UsernameConfiguration> UsernameConfigurations = new()
    {
        new UsernameConfiguration { Key = "MinLengthPolicy", Value = 3 },
        new UsernameConfiguration { Key = "MaxLengthPolicy", Value = 20 },
        new UsernameConfiguration { Key = "OnlyAlphanumericCharacters", IsActive = false}
    };

    public UsernameConfiguration? GetByName(string policyName)
        => UsernameConfigurations.SingleOrDefault(uc => uc.Key.Equals(policyName));

    public IEnumerable<UsernameConfiguration> GetAll()
        => UsernameConfigurations.AsReadOnly();
}

public class UsernameConfiguration
{
    public required string Key { get; set; }
    public object? Value { get; set; }
    public bool IsActive { get; set; } = true;
}