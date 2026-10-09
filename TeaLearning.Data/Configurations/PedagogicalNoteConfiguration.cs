using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class PedagogicalNoteConfiguration : IEntityTypeConfiguration<PedagogicalNote>
{
    public void Configure(EntityTypeBuilder<PedagogicalNote> builder)
    {
        builder.ToTable("PedagogicalNotes");

        builder.HasKey(pn => pn.Id);

        builder.Property(pn => pn.Contenido)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasOne(pn => pn.StudentProfile)
            .WithMany(s => s.PedagogicalNotes)
            .HasForeignKey(pn => pn.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pn => pn.TeacherProfile)
            .WithMany(t => t.PedagogicalNotes)
            .HasForeignKey(pn => pn.TeacherProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pn => new { pn.StudentProfileId, pn.FechaRegistro });
        builder.HasIndex(pn => new { pn.StudentProfileId, pn.VisibleAcudiente, pn.FechaRegistro });

        builder.Property(pn => pn.Titulo)
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty);

        builder.Property(pn => pn.Tipo)
            .HasMaxLength(30)
            .HasDefaultValue("Observacion");

        builder.Property(pn => pn.Area)
            .HasMaxLength(30)
            .HasDefaultValue("Socioemocional");

        builder.Property(pn => pn.RecomendacionCasa)
            .HasMaxLength(500);

        builder.Property(pn => pn.VisibleAcudiente)
            .HasDefaultValue(true);
    }
}
