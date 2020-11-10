using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.IO;

namespace DiscordWoL
{
    /***************************************************************************
    MIT License
    Copyright (c) 2020 imerzan

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
    ****************************************************************************/
    public static class Wol
    {
        /// <summary>
        /// Builds & Sends a Wake-On-Lan packet
        /// </summary>
        /// <param name="macaddress">MacAddress in any standard HEX format</param>
        /// <returns></returns>
        public static int Send(string macaddress)
        {
            try
            {
                using Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                {
                    EnableBroadcast = true
                };

                // Enable broadcast, required for macOS compatibility 
                IPEndPoint ep1 = new IPEndPoint(IPAddress.Broadcast, 7); // Port 7 common WOL port
                IPEndPoint ep2 = new IPEndPoint(IPAddress.Broadcast, 9); // Port 9 common WOL port

                // Get magic packet byte array based on MAC Address
                byte[] mp = BuildMagicPacket(macaddress); 

                if (mp == null)
                {
                    throw new NullReferenceException("Magic Packet value is null. Please verify MAC Address is entered/formatted correctly.");
                }

                // Transmit Magic Packet on Port 7
                sock.SendTo(mp, ep1);
                // Transmit Magic Packet on Port 9
                sock.SendTo(mp, ep2);
                // Close socket
                sock.Close();

                return 0;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// Builds the Wake-On-Lan packet (duh)
        /// </summary>
        /// <param name="macaddress">MacAddress in any standard HEX format</param>
        /// <returns></returns>
        private static byte[] BuildMagicPacket(string macaddress) 
        {
            try
            {
                macaddress = Regex.Replace(macaddress, "[: -]", "");
                byte[] macBytes = new byte[6];

                for (int i = 0; i < 6; i++)
                {
                    macBytes[i] = Convert.ToByte(macaddress.Substring(i * 2, 2), 16);
                }

                using MemoryStream ms = new MemoryStream();

                using BinaryWriter bw = new BinaryWriter(ms);

                // First 6 times 0xff
                for (int i = 0; i < 6; i++)
                {
                    bw.Write((byte)0xff);
                }

                // Then 16 times MacAddress
                for (int i = 0; i < 16; i++)
                {
                    bw.Write(macBytes);
                }

                // return 102 bytes magic packet
                return ms.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
