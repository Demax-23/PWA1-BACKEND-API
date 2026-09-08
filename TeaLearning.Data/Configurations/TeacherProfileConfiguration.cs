using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class TeacherProfileConfiguration : IEntityTypeConfiguration<TeacherProfile>
{
    public void Configure(EntityTypeBuilder<TeacherProfile> builder)
    {
        builder.ToTable("TeacherProfiles");

        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.UserId)
            .IsUnique();

        builder.Property(t => t.Especialidad)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.NumeroRegistroProfesional)
            .HasMaxLength(50);
    }
}
