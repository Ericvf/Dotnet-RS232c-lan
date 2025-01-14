﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RS232cTcpSharp
{
    public sealed class RS232cTcpSharpClient : IRS232cTcpSharpClient, IDisposable
    {
        private readonly ILogger<RS232cTcpSharpClient> logger;
        private readonly Dictionary<Commands, string> _commandTexts = new Dictionary<Commands, string>()
        {
            { Commands.PowerControl, "POWR" },
            { Commands.InputModeSelection, "INPS" },
            { Commands.Brightness, "VLMP" },
            { Commands.Size, "WIDE" },
            { Commands.ColorMode, "BMOD" },
            { Commands.WhiteBalance, "WHBL" },
            { Commands.RContrast, "CRTR" },
            { Commands.GContrast, "CRTG" },
            { Commands.BContrast, "CRTB" },
            { Commands.TelnetUsername, "USER" },
            { Commands.TelnetPassword, "PASS" },
            { Commands.ThermalSensorSetting, "STDR" },
            { Commands.Model, "INF1" },
            { Commands.SerialNo, "SRNO" },
            { Commands.Auto, "ASNC" },
            { Commands.Reset, "RSET" },
            { Commands.Volume, "VOLM" },
            { Commands.Mute, "MUTE" },
            { Commands.TemperatureSensor, "DSTA" },
            { Commands.Temperature, "ERRT" },
        };
        private const int timeout = 200;

        private TcpClient? tcpClient;
        private NetworkStream? networkStream;
        private StreamWriter? writer;

        public RS232cTcpSharpClient(ILogger<RS232cTcpSharpClient> logger)
        {
            this.logger = logger;
        }

        public async Task Start(string ipAddress, int port, string? username = null, string? password = null)
        {
            tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(ipAddress, port);

            networkStream = tcpClient.GetStream();
            writer = new StreamWriter(networkStream);

            writer.AutoFlush = true;
            networkStream.ReadTimeout = 500;

            string input, output;

            output = ReadOutput();
            logger.LogInformation(output);

            if (output == "Login:")
            {
                input = string.Empty;
                writer.WriteLine(input);
                output = ReadOutput();
                logger.LogInformation(output);
            }

            if (output == "\r\nPassword:")
            {
                input = string.Empty;
                writer.WriteLine(input);

                output = ReadOutput();
                logger.LogInformation(output);
            }
        }

        public void Stop()
        {
            SendCommandAndGetResponse("BYE");
            networkStream?.Dispose();
        }

        public string Get(Commands command)
        {
            var commandString = GetCommandString(command);
            return SendCommandAndGetResponse($"{commandString}????");
        }

        public string Set(string command, string value)
            => SendCommandAndGetResponse($"{command}{Pad(value)}");

        public string Set(Commands command, int value)
        {
            var commandString = GetCommandString(command);
            return SendCommandAndGetResponse($"{commandString}{Pad(value.ToString())}");
        }

        private string GetCommandString(Commands command) => _commandTexts[command];

        public string Get(string command) => SendCommandAndGetResponse(command);

        private string Pad(string value) => value.PadLeft(4, '0');

        private string SendCommandAndGetResponse(string command)
        {
            logger.LogInformation("SendCommandAndGetResponse", command);

            writer!.WriteLine(command);
            var output = ReadOutput();

            if (output == "WAIT\r\n")
            {
                output = ReadOutput();
            }

            return output;
        }

        private string ReadOutput()
        {
            if (networkStream == null)
                return string.Empty;

            var stringBuilder = new StringBuilder();

            using var cancellationToken = new CancellationTokenSource();
            cancellationToken.CancelAfter(timeout);

            while (!networkStream.DataAvailable && !cancellationToken.IsCancellationRequested) ;
            while (!cancellationToken.IsCancellationRequested && networkStream.DataAvailable)
            {
                stringBuilder.Append((char)networkStream.ReadByte());
            }

            return stringBuilder.ToString();
        }

        public bool IsConnected() => tcpClient?.Connected == true;

        public void Dispose()
        {
            networkStream?.Dispose();
        }

    }
}