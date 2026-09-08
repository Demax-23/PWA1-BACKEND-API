using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class ActivityAssignmentConfiguration : IEntityTypeConfiguration<ActivityAssignment>
{
    public void Configure(EntityTypeBuilder<ActivityAssignment> builder)
    {
        builder.ToTable("ActivityAssignments");

        builder.HasKey(aa => aa.Id);

        builder.Property(aa => aa.Prioridad)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(aa => aa.Estado)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasOne(aa => aa.Activity)
            .WithMany(a => a.Assignments)
            .HasForeignKey(aa => aa.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(aa => aa.StudentProfile)
            .WithMany(s => s.Assignments)
            .HasForeignKey(aa => aa.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(aa => new { aa.StudentProfileId, aa.FechaInicio, aa.OrdenSecuencial });
    }
}
