using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("StudentProfiles");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CodigoEstudiante)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.CodigoEstudiante)
            .IsUnique();

        builder.HasIndex(s => s.UserId)
            .IsUnique();

        builder.Property(s => s.ObservacionesMedicasTEA)
            .HasMaxLength(1000);

        builder.HasOne(s => s.Classroom)
            .WithMany(c => c.Students)
            .HasForeignKey(s => s.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.GuardianProfile)
            .WithMany(g => g.Students)
            .HasForeignKey(s => s.GuardianProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SensoryPreference)
            .WithOne(sp => sp.StudentProfile)
            .HasForeignKey<SensoryPreference>(sp => sp.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
