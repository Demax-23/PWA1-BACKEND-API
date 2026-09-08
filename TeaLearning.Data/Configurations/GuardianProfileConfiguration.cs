using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class GuardianProfileConfiguration : IEntityTypeConfiguration<GuardianProfile>
{
    public void Configure(EntityTypeBuilder<GuardianProfile> builder)
    {
        builder.ToTable("GuardianProfiles");

        builder.HasKey(g => g.Id);

        builder.HasIndex(g => g.UserId)
            .IsUnique();

        builder.Property(g => g.TelefonoContacto)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(g => g.Parentesco)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(g => g.Direccion)
            .HasMaxLength(250);
    }
}
