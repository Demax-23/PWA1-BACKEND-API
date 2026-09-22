using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class LearningModuleConfiguration : IEntityTypeConfiguration<LearningModule>
{
    public void Configure(EntityTypeBuilder<LearningModule> builder)
    {
        builder.ToTable("LearningModules");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Titulo)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(m => m.Descripcion)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.Icono)
            .HasMaxLength(100);

        builder.HasOne(m => m.Categoria)
            .WithMany(c => c.Modules)
            .HasForeignKey(m => m.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Activities)
            .WithOne(a => a.Modulo)
            .HasForeignKey(a => a.ModuloId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
