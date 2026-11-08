using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class ActivityExecutionConfiguration : IEntityTypeConfiguration<ActivityExecution>
{
    public void Configure(EntityTypeBuilder<ActivityExecution> builder)
    {
        builder.ToTable("ActivityExecutions", tb =>
        {
            tb.HasTrigger("TR_ActivityExecutions_AfterInsert_SyncAndAlert");
        });

        builder.HasKey(ae => ae.Id);

        builder.HasOne(ae => ae.Assignment)
            .WithMany(a => a.Executions)
            .HasForeignKey(ae => ae.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ae => ae.StudentProfile)
            .WithMany(s => s.Executions)
            .HasForeignKey(ae => ae.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ae => new { ae.StudentProfileId, ae.FechaRegistro });
    }
}
