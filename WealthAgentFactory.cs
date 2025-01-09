using wealthai;

public class WealthAgentFactory
{
    private readonly IServiceProvider _serviceProvider;

    public WealthAgentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public WealthAgent Create()
    {
        return _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<WealthAgent>();
    }
}

