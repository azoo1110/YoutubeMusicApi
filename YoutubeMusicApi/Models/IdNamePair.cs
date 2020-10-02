﻿using System;
using System.Collections.Generic;
using System.Text;

namespace YoutubeMusicApi.Models
{
    public class IdNamePair
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public IdNamePair(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
