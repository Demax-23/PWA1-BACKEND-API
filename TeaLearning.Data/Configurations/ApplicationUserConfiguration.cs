using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers");

        builder.Property(u => u.Nombres)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Apellidos)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.RefreshToken)
            .HasMaxLength(500);

        builder.HasOne(u => u.StudentProfile)
            .WithOne(s => s.User)
            .HasForeignKey<StudentProfile>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.TeacherProfile)
            .WithOne(t => t.User)
            .HasForeignKey<TeacherProfile>(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.GuardianProfile)
            .WithOne(g => g.User)
            .HasForeignKey<GuardianProfile>(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
