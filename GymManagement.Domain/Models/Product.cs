using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymManagement.Domain.Models
{
    public class Product
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? Image { get; set; }
        public double Rating { get; set; }
        public int Quantity { get; set; }

    }
}
