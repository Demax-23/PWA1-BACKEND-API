using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeaLearning.Core.Entities;

namespace TeaLearning.Data.Configurations;

public class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.ToTable("Classrooms");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Grado)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(c => c.TeacherProfile)
            .WithMany(t => t.Classrooms)
            .HasForeignKey(c => c.TeacherProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
