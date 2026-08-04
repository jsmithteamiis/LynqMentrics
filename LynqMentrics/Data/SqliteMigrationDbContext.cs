using Microsoft.EntityFrameworkCore;

namespace LynqMentrics.Data;

public sealed class SqliteMigrationDbContext(DbContextOptions<SqliteMigrationDbContext> options)
    : AppDbContext(options)
{
}
