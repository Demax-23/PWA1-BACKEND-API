using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class LearningCategoryConfiguration : IEntityTypeConfiguration<LearningCategory>
{
    public void Configure(EntityTypeBuilder<LearningCategory> builder)
    {
        builder.ToTable("LearningCategories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasIndex(c => c.Nombre);

        builder.Property(c => c.Descripcion)
            .HasMaxLength(500);

        builder.Property(c => c.Color)
            .HasMaxLength(30)
            .HasDefaultValue("#2F6E5C");

        builder.Property(c => c.Orden)
            .HasDefaultValue(1);

        builder.HasMany(c => c.Modules)
            .WithOne(m => m.Categoria)
            .HasForeignKey(m => m.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
