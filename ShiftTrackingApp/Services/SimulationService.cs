using Microsoft.EntityFrameworkCore;
using ShiftTrackingApp.Data;
using ShiftTrackingApp.DTOs;
using ShiftTrackingApp.Helpers;
using ShiftTrackingApp.Models;
using ShiftTrackingApp.Services.Interfaces;

namespace ShiftTrackingApp.Services
{
    /// <summary>
    /// Konfeksiyon atölyesi için gerçekçi demo verisi üretir.
    /// Adımlar: (1) ihtiyaç matrisini kadroyla örtüşen değerlere sıfırla,
    /// (2) günlük roster atamaları (rotasyon + izin günleri + planlama açığı),
    /// (3) devam/check-in (~%88 gelen → bazı eksikler), (4) eksikler için
    /// yevmiyeci çağrıları (~%70 kabul → otomatik atama + geliş).
    /// </summary>
    public class SimulationService : ISimulationService
    {
        private readonly AppDbContext _db;
        public SimulationService(AppDbContext db) => _db = db;

        // Gerçekçi, kadroyla örtüşen hafta içi ihtiyaç: (dept, Sabah, Öğle, Gece)
        // Kadro: Kesim 4, Dikiş 6, Ütü&Pkt 4, Kalite 3, Sevkiyat 3
        private static readonly (int dept, int s, int o, int g)[] Req =
        {
            (1, 2, 1, 0),  // Kesim     → 3/gün (4 kişiden 1 rotasyon izni)
            (2, 3, 1, 1),  // Dikiş     → 5/gün (6 kişiden 1 rotasyon izni)
            (3, 2, 1, 0),  // Ütü & Pkt → 3/gün
            (4, 1, 1, 0),  // Kalite    → 2/gün
            (5, 1, 1, 0),  // Sevkiyat  → 2/gün
        };

        public async Task<SimulationResultDto> GenerateAsync(DateOnly start, DateOnly end)
        {
            var rnd = new Random(42);
            var res = new SimulationResultDto();

            var startDt = start.ToDateTime(TimeOnly.MinValue);
            var endExcl = end.AddDays(1).ToDateTime(TimeOnly.MinValue);

            // ── 1) Aralıktaki eski veriyi temizle (re-run güvenli) ──────────
            _db.ShiftAssignments.RemoveRange(
                await _db.ShiftAssignments.Where(a => a.Date >= start && a.Date <= end).ToListAsync());
            _db.AttendanceLogs.RemoveRange(
                await _db.AttendanceLogs.Where(l => l.CheckIn >= startDt && l.CheckIn < endExcl).ToListAsync());
            _db.CasualCallouts.RemoveRange(
                await _db.CasualCallouts.Where(c => c.Date >= start && c.Date <= end).ToListAsync());
            await _db.SaveChangesAsync();

            // ── 2) İhtiyaç matrisini gerçekçi değerlerle yeniden kur ────────
            _db.StaffingRequirements.RemoveRange(await _db.StaffingRequirements.ToListAsync());
            foreach (var (dept, s, o, g) in Req)
                foreach (var (shiftId, baseCount) in new[] { (1, s), (2, o), (3, g) })
                {
                    if (baseCount <= 0) continue;
                    for (int dow = 0; dow < 7; dow++)
                    {
                        int req = dow >= 5 ? (baseCount + 1) / 2 : baseCount;
                        if (req <= 0) continue;
                        _db.StaffingRequirements.Add(new StaffingRequirement
                        {
                            DepartmentId = dept, ShiftId = shiftId, DayOfWeek = dow,
                            RequiredCount = req, UpdatedAt = DateTime.UtcNow
                        });
                        res.StaffingRows++;
                    }
                }
            await _db.SaveChangesAsync();

            // ── Kadro & yevmiyeci havuzları (departman bazında) ─────────────
            var perm = await _db.Users
                .Where(u => u.IsActive && u.EmploymentType == "Permanent"
                         && u.Role == "Employee" && u.DepartmentId != null)
                .Select(u => new { u.Id, Dept = u.DepartmentId!.Value })
                .ToListAsync();
            var permByDept = perm.GroupBy(u => u.Dept)
                .ToDictionary(grp => grp.Key, grp => grp.Select(u => u.Id).OrderBy(x => x).ToList());

            var casual = await _db.Users
                .Where(u => u.IsActive && u.EmploymentType == "Casual" && u.DepartmentId != null)
                .Select(u => new { u.Id, Dept = u.DepartmentId!.Value })
                .ToListAsync();
            var casualByDept = casual.GroupBy(u => u.Dept)
                .ToDictionary(grp => grp.Key, grp => grp.Select(u => u.Id).ToList());

            var shifts = await _db.Shifts.Where(s => s.Id <= 3).ToDictionaryAsync(s => s.Id);

            var assignments = new List<ShiftAssignment>();
            var attendance  = new List<AttendanceLog>();
            var callouts    = new List<CasualCallout>();

            // Gün/dept/vardiya bazında atanan + gelen takibi (çağrı hesabı için)
            // key: (date, dept, shift) → (assigned, present)
            var coverageTrack = new Dictionary<(DateOnly, int, int), (int assigned, int present)>();
            var busyOnDate    = new HashSet<(int userId, DateOnly date)>(); // çift-atama engeli

            AttendanceLog MakeCheckIn(int uid, DateOnly d, int shiftId)
            {
                var st = shifts[shiftId].StartTime;
                // Çoğu personel zamanında (-6..+4 dk), ~%6 geç (+10..35 dk)
                bool late = rnd.NextDouble() < 0.06;
                int offset = late ? rnd.Next(10, 36) : rnd.Next(-6, 5);
                // Türkiye yerel check-in saati → UTC olarak sakla (sistem UTC saklıyor,
                // gösterimde +3 çevriliyor). Böylece ekranda doğru saat görünür.
                var turkeyCi = d.ToDateTime(TimeOnly.FromTimeSpan(st)).AddMinutes(offset);
                var ci = TimeZoneHelper.ConvertToUtc(turkeyCi);
                return new AttendanceLog { UserId = uid, CheckIn = ci, CheckOut = ci.AddHours(8), Source = "Manual", Note = "[SIM]" };
            }

            int dayIdx = 0;
            for (var d = start; d <= end; d = d.AddDays(1), dayIdx++)
            {
                int dow = ((int)d.DayOfWeek + 6) % 7;
                bool weekend = dow >= 5;
                res.Days++;

                foreach (var (dept, s0, o0, g0) in Req)
                {
                    if (!permByDept.TryGetValue(dept, out var emps) || emps.Count == 0) continue;

                    int s = weekend ? (s0 + 1) / 2 : s0;
                    int o = weekend ? (o0 + 1) / 2 : o0;
                    int g = weekend ? (g0 + 1) / 2 : g0;

                    var slots = new List<int>();
                    for (int k = 0; k < s; k++) slots.Add(1);
                    for (int k = 0; k < o; k++) slots.Add(2);
                    for (int k = 0; k < g; k++) slots.Add(3);
                    if (slots.Count == 0) continue;

                    // Rotasyon: güne göre çalışan/izinli kişiler değişsin
                    int n = emps.Count;
                    int rot = (dayIdx * 2 + dept) % n;
                    var rotated = emps.Skip(rot).Concat(emps.Take(rot)).ToList();

                    int assignCount = Math.Min(rotated.Count, slots.Count);
                    // ~%18 ihtimalle bir slotu boş bırak (planlama açığı)
                    if (assignCount > 0 && rnd.NextDouble() < 0.18) assignCount--;

                    for (int k = 0; k < assignCount; k++)
                    {
                        int uid = rotated[k];
                        int shiftId = slots[k];
                        if (busyOnDate.Contains((uid, d))) continue;
                        busyOnDate.Add((uid, d));

                        assignments.Add(new ShiftAssignment
                        {
                            UserId = uid, ShiftId = shiftId, Date = d,
                            Note = "[SIM]", CreatedAt = DateTime.UtcNow
                        });
                        res.Assignments++;

                        var key = (d, dept, shiftId);
                        var cur = coverageTrack.GetValueOrDefault(key);
                        cur.assigned++;

                        // Devam: ~%88 gelen, ~%12 gelmez (eksik üretir)
                        if (rnd.NextDouble() < 0.88)
                        {
                            attendance.Add(MakeCheckIn(uid, d, shiftId));
                            res.Attendance++;
                            cur.present++;
                        }
                        else res.Absences++;

                        coverageTrack[key] = cur;
                    }
                }
            }

            // ── 4) Eksikler için yevmiyeci çağrıları ────────────────────────
            // Her (gün, dept, vardiya) için: eksik = gereken - gelen.
            var reqLookup = new Dictionary<(int dept, int shift, int dow), int>();
            foreach (var (dept, s, o, g) in Req)
            {
                for (int dow = 0; dow < 7; dow++)
                {
                    bool we = dow >= 5;
                    reqLookup[(dept, 1, dow)] = we ? (s + 1) / 2 : s;
                    reqLookup[(dept, 2, dow)] = we ? (o + 1) / 2 : o;
                    reqLookup[(dept, 3, dow)] = we ? (g + 1) / 2 : g;
                }
            }

            // Yevmiyecinin o gün meşgul olup olmadığını izle
            var casualBusy = new HashSet<(int userId, DateOnly date)>();

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                int dow = ((int)d.DayOfWeek + 6) % 7;
                foreach (var (dept, _, _, _) in Req)
                {
                    if (!casualByDept.TryGetValue(dept, out var pool) || pool.Count == 0) continue;
                    foreach (int shiftId in new[] { 1, 2, 3 })
                    {
                        int required = reqLookup.GetValueOrDefault((dept, shiftId, dow));
                        if (required <= 0) continue;
                        var cur = coverageTrack.GetValueOrDefault((d, dept, shiftId));
                        int shortage = required - cur.present;
                        if (shortage <= 0) continue;

                        // Eksik kadar (havuz elverdiğince) çağrı dene
                        var available = pool.Where(id => !casualBusy.Contains((id, d))).ToList();
                        int tries = Math.Min(shortage, available.Count);
                        for (int i = 0; i < tries; i++)
                        {
                            // Her eksik slot ~%75 ihtimalle çağrı alır
                            if (rnd.NextDouble() > 0.75) continue;
                            int uid = available[i];
                            casualBusy.Add((uid, d));

                            bool accepted = rnd.NextDouble() < 0.70;
                            var callout = new CasualCallout
                            {
                                DepartmentId = dept, ShiftId = shiftId, Date = d,
                                CalledUserId = uid, CreatedBy = 1,
                                Status = accepted ? "Accepted" : "Rejected",
                                CreatedAt = d.ToDateTime(new TimeOnly(7, 30)),
                                RespondedAt = d.ToDateTime(new TimeOnly(7, 45)),
                                Note = "[SIM]"
                            };
                            callouts.Add(callout);
                            res.Callouts++;

                            if (accepted)
                            {
                                res.CalloutsAccepted++;
                                assignments.Add(new ShiftAssignment
                                {
                                    UserId = uid, ShiftId = shiftId, Date = d,
                                    Note = "[SIM] yevmiyeci çağrısı", CreatedAt = DateTime.UtcNow
                                });
                                // Kabul eden yevmiyeci ~%92 gelir
                                if (rnd.NextDouble() < 0.92)
                                    attendance.Add(MakeCheckIn(uid, d, shiftId));
                            }
                            else res.CalloutsRejected++;
                        }
                    }
                }
            }

            _db.ShiftAssignments.AddRange(assignments);
            _db.AttendanceLogs.AddRange(attendance);
            _db.CasualCallouts.AddRange(callouts);
            await _db.SaveChangesAsync();

            return res;
        }
    }
}
