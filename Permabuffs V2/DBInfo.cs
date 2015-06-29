using System;
using System.Collections.Generic;

namespace Permabuffs_V2
{
    public class DBInfo
    {
        public List<int> bufflist;

        public DBInfo(string activeBuffs)
        {
            bufflist = new List<int>();

            string[] buffstring = null;

            if (activeBuffs != "")
            {
                buffstring = activeBuffs.Split(',');
                foreach (string buff in buffstring)
                {
                    int tempbuff = -1;
                    if(Int32.TryParse(buff, out tempbuff))
                        bufflist.Add(tempbuff);
                }
            }
        }
    }
}
