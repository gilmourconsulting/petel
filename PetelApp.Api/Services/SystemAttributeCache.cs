using System.Collections.Generic;
using PetelApp.Api.Models;

namespace PetelApp.Api.Services
{
    public class SystemAttributeCache
    {
        public List<SystemAttributeDto> Attributes { get; set; } = new();
    }
}