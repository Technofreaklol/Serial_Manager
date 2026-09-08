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

    public void SaveMachine(string name)
    {
        using var db = DbContextFactory.Create();

        var machine = db.Machines
                        .FirstOrDefault(m => m.Name == name);

        if (machine == null)
        {
            db.Machines.Add(new Machine
            {
                Name = name
            });

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