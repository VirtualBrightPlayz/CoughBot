using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace CoughBot
{
    public class StatsDraw
    {
        public static void Draw(string path, SocketGuild guild, Dictionary<string, List<ulong>> data, int maxListings)
        {
            using (Bitmap bm = new Bitmap(600, 600))
            using (Graphics gfx = Graphics.FromImage(bm))
            using (Pen pen = new Pen(Color.White, 100f))
            using (SolidBrush brush = new SolidBrush(Color.White))
            using (Font font = new Font(FontFamily.GenericMonospace, 12f))
            using (Font fontTitle = new Font(FontFamily.GenericMonospace, 25f))
            {
                Random rng = new Random();
                gfx.Clear(Color.Black);
                gfx.DrawString("People Infected", fontTitle, brush, 10f, 10f);
                Rectangle rect = new Rectangle(100, 50, 400, 300);
                var data2 = data.OrderBy(p => p.Value.Count).ToList();
                // data2.RemoveRange(Math.Clamp(data2.Count, 0, maxListings), Math.Clamp(data2.Count, 0, maxListings));
                int total = 0;
                foreach (var item in data2)
                {
                    total += item.Value.Count;
                }
                float ang = 0f;
                Dictionary<string, Color> colors = new Dictionary<string, Color>();
                foreach (var item in data2)
                {
                    Color color = Color.FromArgb(rng.Next(50, 255), rng.Next(50, 255), rng.Next(50, 255));
                    colors.Add(item.Key, color);
                    pen.Color = color;
                    brush.Color = color;
                    gfx.FillPie(brush, rect, ang / (float)total * 360f, item.Value.Count / (float)total * 360f);
                    ang += item.Value.Count;
                }
                float line = 10f;
                float vert = 410f;
                foreach (var item in colors)
                {
                    var user = guild.GetUser(ulong.Parse(item.Key));
                    string name = user.Username;
                    if (!string.IsNullOrWhiteSpace(user.Nickname))
                        name = user.Nickname;
                    name += $"#{user.Discriminator} - {data[item.Key].Count}";
                    brush.Color = item.Value;
                    var dims = gfx.MeasureString(name, font);
                    if (line + dims.Width >= 590f)
                    {
                        vert += dims.Height;
                        line = 10f;
                    }
                    gfx.DrawString($"{name}", font, brush, line, vert);
                    line += dims.Width;
                }
                bm.Save(path, ImageFormat.Png);
            }
        }
    }
}