using Microsoft.EntityFrameworkCore;

namespace LynqMentrics.Data;

public sealed class PostgresMigrationDbContext(DbContextOptions<PostgresMigrationDbContext> options)
    : AppDbContext(options)
{
}
