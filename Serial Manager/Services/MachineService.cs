using SerialManager.Data;
using SerialManager.Models;

namespace SerialManager.Services;

public class MachineService
{
    public List<Machine> GetMachines()
    {
        using var db = DbContextFactory.Create();

        return db.Machines
                 .OrderBy(m => m.Name)
                 .ToList();
    }

    public Machine? GetMachine(string name)
    {
        using var db = DbContextFactory.Create();

        return db.Machines
                 .FirstOrDefault(m => m.Name == name);
    }

    public Machine? GetMachine(int id)
    {
        using var db = DbContextFactory.Create();

        return db.Machines
                 .FirstOrDefault(m => m.Id == id);
    }

    public void SaveMachine(int? id, string name)
    {
        using var db = DbContextFactory.Create();

        if (id.HasValue)
        {
            var machine = db.Machines.Find(id.Value)
                ?? throw new Exception("Maschine wurde nicht gefunden.");

            if (db.Machines.Any(m => m.Id != id.Value && m.Name == name))
                throw new Exception("Dieser Maschinenname wird bereits verwendet.");

            var oldName = machine.Name;
            machine.Name = name;

            db.SaveChanges();

            // Historie ist nur über den Maschinennamen verknüpft
            // (keine echte Fremdschlüssel-Beziehung) – bei Umbenennung mitziehen.
            if (oldName != name)
            {
                var relatedHistory = db.SerialHistories
                    .Where(h => h.Machine == oldName)
                    .ToList();

                foreach (var entry in relatedHistory)
                    entry.Machine = name;

                db.SaveChanges();
            }
        }
        else
        {
            if (db.Machines.Any(m => m.Name == name))
                throw new Exception("Dieser Maschinenname existiert bereits.");

            db.Machines.Add(new Machine { Name = name });
            db.SaveChanges();
        }
    }

    public void DeleteMachine(int id)
    {
        using var db = DbContextFactory.Create();

        var machine = db.Machines.Find(id);

        if (machine == null)
            return;

        db.Machines.Remove(machine);

        db.SaveChanges();
    }
}