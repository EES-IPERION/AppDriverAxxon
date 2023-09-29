using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDriverAxxon
{
    public class Camera
    {
        public string friendlyNameLong { get; set; }
        public string friendlyNameShort { get; set; }
        public string id { get; set; }
        public string origin { get; set; }
        public string state { get; set; }
        public string rtsp { get; set; }
        public bool isSelected { get; set; }

        Camera()
        {
            isSelected = false;
        }

        public bool Equals(Camera camera)
        {
            if (this.origin == camera.origin)
            {
                return true;
            }
            return false;
        }

        public String toString()
        {
            return $"\nId : {id}\nLong name : {friendlyNameLong}\nShort name :{friendlyNameShort}\nOrigin : {origin}\nState : {state}\nisSelected : {isSelected}";
        }
    }
}
