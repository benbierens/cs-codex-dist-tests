﻿using ArgsUniform;

namespace BiblioTech
{
    public class Configuration
    {
        [Uniform("token", "t", "TOKEN", true, "Discord Application Token")]
        public string ApplicationToken { get; set; } = string.Empty;

        [Uniform("server-name", "sn", "SERVERNAME", true, "Name of the Discord server")]
        public string ServerName { get; set; } = string.Empty;

        [Uniform("endpoints", "e", "ENDPOINTS", false, "Path where endpoint JSONs are located. Also accepts codex-deployment JSONs.")]
        public string EndpointsPath { get; set; } = "endpoints";

        [Uniform("userdata", "u", "USERDATA", false, "Path where user data files will be saved.")]
        public string UserDataPath { get; set; } = "userdata";

        [Uniform("admin-role", "a", "ADMINROLE", true, "Name of the Discord server admin role")]
        public string AdminRoleName { get; set; } = string.Empty;

        [Uniform("admin-channel-name", "ac", "ADMINCHANNELNAME", true, "Name of the Discord server channel where admin commands are allowed.")]
        public string AdminChannelName { get; set; } = "admin-channel";
    }
}
