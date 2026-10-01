using EventTicketing.Api.Entities;

namespace EventTicketing.Api.Data;

public static class EventDbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Events.Any())
        {
            return;
        }

        var now = DateTime.UtcNow;

        // 25 Published events with OnSale showtimes in the future
        for (int i = 1; i <= 25; i++)
        {
            var ev = new Event
            {
                Name = $"Sự kiện âm nhạc mùa hè {i}",
                Description = $"Mô tả chi tiết cho sự kiện âm nhạc mùa hè {i}. Hứa hẹn mang đến nhiều trải nghiệm thú vị.",
                Location = $"Nhà hát Lớn Hà Nội, phòng số {i}",
                ImageUrl = i % 2 == 0 ? $"https://example.com/images/event-{i}.jpg" : null,
                Status = EventStatus.Published,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-10)
            };

            ev.Showtimes.Add(new Showtime
            {
                StartTime = now.AddDays(i),
                EndTime = now.AddDays(i).AddHours(2),
                Status = ShowtimeStatus.OnSale
            });

            // Add an extra showtime for some
            if (i % 3 == 0)
            {
                ev.Showtimes.Add(new Showtime
                {
                    StartTime = now.AddDays(i + 1),
                    EndTime = now.AddDays(i + 1).AddHours(2),
                    Status = ShowtimeStatus.OnSale
                });
            }

            db.Events.Add(ev);
        }

        // Draft events
        for (int i = 1; i <= 3; i++)
        {
            var ev = new Event
            {
                Name = $"Sự kiện sắp ra mắt {i}",
                Description = $"Sự kiện đang trong quá trình lên kế hoạch {i}.",
                Location = "Trung tâm Hội nghị Quốc gia",
                ImageUrl = null,
                Status = EventStatus.Draft,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2)
            };

            ev.Showtimes.Add(new Showtime
            {
                StartTime = now.AddDays(10 + i),
                EndTime = now.AddDays(10 + i).AddHours(3),
                Status = ShowtimeStatus.Draft
            });

            db.Events.Add(ev);
        }

        // Ended events
        for (int i = 1; i <= 2; i++)
        {
            var ev = new Event
            {
                Name = $"Sự kiện đã kết thúc {i}",
                Description = $"Sự kiện đã diễn ra thành công tốt đẹp {i}.",
                Location = "Sân vận động Mỹ Đình",
                ImageUrl = $"https://example.com/images/ended-{i}.jpg",
                Status = EventStatus.Ended,
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-30)
            };

            ev.Showtimes.Add(new Showtime
            {
                StartTime = now.AddDays(-10 + i),
                EndTime = now.AddDays(-10 + i).AddHours(4),
                Status = ShowtimeStatus.Closed
            });

            db.Events.Add(ev);
        }

        db.SaveChanges();
    }
}
