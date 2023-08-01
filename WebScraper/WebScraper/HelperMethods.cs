using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebScraper {
    internal class HelperMethods {
        public static void printDataTable(DataTable tbl) {
            string line = "";
            int limit = 0;
            foreach(DataColumn item in tbl.Columns) {
                limit += 1;
                line += item.ColumnName + "   ";

                if(limit == 12) break;
            }
            line += "\n";
            foreach(DataRow row in tbl.Rows) {
                limit = 0;
                for(int i = 0; i < tbl.Columns.Count; i++) {
                    line += row[i].ToString() + "   ";
                    limit += 1;
                    if(limit == 12) break;
                }
                line += "\n";
            }
            Console.WriteLine(line);
        }
    }
}
