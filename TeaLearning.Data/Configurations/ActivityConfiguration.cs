using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Titulo)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Descripcion)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.TipoActividad)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.ObjetivoPedagogico)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.ConfigJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.HasOne(a => a.Modulo)
            .WithMany(m => m.Activities)
            .HasForeignKey(a => a.ModuloId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Docente)
            .WithMany(t => t.ActivitiesCreated)
            .HasForeignKey(a => a.DocenteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
