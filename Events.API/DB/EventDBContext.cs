using System;
using Events.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Events.API.DB;

public class EventDBContext : DbContext
{ 
    public EventDBContext(DbContextOptions opts) : base(opts) { }

    public DbSet<Event> Events { get; set; }
}

