using System.Collections.Generic;
using Aevatar.Agents.Creator;

namespace Aevatar.Subscription;

public class EventDescriptionDto
{
    public string EventType { get; set; }
    public string Description { get; set; }
    public List<EventProperty> EventProperties { get; set; }
}
