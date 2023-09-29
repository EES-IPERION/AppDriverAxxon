using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDriverAxxon
{
    public class Object
    {
        public string id { get; set; } = null;
        public string name { get; set; } = null;
        public string type { get; set; } = null;
        public string state { get; set; } = null;

        public String toString()
        {
            return $"\nName : {name}\ntype : {type}\nState : {state}";
        }
    }

    public class ObjectsList
    {
        public List<Object> objects { get; set; }
    }

}
