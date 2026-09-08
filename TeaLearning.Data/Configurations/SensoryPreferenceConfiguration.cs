using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class SensoryPreferenceConfiguration : IEntityTypeConfiguration<SensoryPreference>
{
    public void Configure(EntityTypeBuilder<SensoryPreference> builder)
    {
        builder.ToTable("SensoryPreferences");

        builder.HasKey(sp => sp.Id);

        builder.HasIndex(sp => sp.StudentProfileId)
            .IsUnique();

        builder.Property(sp => sp.PaletaColor)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sp => sp.Tipografia)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sp => sp.NivelEscalaFuente)
            .IsRequired();

        builder.Property(sp => sp.ModoContraste)
            .HasConversion<string>()
            .HasMaxLength(30);
    }
}
