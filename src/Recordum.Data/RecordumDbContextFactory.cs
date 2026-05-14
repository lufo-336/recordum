using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Recordum.Data;

public sealed class RecordumDbContextFactory : IDesignTimeDbContextFactory<RecordumDbContext>
{
    public RecordumDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite("Data Source=recordum-design.db")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new RecordumDbContext(options);
    }
}
