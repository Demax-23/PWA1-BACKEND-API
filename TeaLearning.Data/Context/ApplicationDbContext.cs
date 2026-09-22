using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Context;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<GuardianProfile> GuardianProfiles => Set<GuardianProfile>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<SensoryPreference> SensoryPreferences => Set<SensoryPreference>();
    public DbSet<LearningCategory> LearningCategories => Set<LearningCategory>();
    public DbSet<LearningModule> LearningModules => Set<LearningModule>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityAssignment> ActivityAssignments => Set<ActivityAssignment>();
    public DbSet<ActivityExecution> ActivityExecutions => Set<ActivityExecution>();
    public DbSet<PedagogicalNote> PedagogicalNotes => Set<PedagogicalNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
