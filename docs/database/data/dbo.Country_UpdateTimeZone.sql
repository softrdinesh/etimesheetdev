/* =============================================================================
   dbo.Country — populate the IANA time zone column
   -----------------------------------------------------------------------------
   Source     : tzdata zone.tab (IANA time zone database)
   Matches on : Country.Code (ISO 3166-1 alpha-2)
   Value      : one IANA zone id, or several comma-separated (no spaces) when the
                country spans more than one zone. The first id is the country's
                primary/most-populous zone, so a client may take it as the default.
   Idempotent : re-running it simply rewrites the same values.

   Two adjustments to make before running, if your schema differs:
     - table  dbo.Country
     - column TimeZone
   Both appear once, in the UPDATE ... FROM below.

   Column width: the longest value is 598 characters (US, 29 zones), followed by
   CA (409) and RU (385). TimeZone must therefore hold at least 600 characters,
   and step 1 below widens it to 1000 if it is narrower -- otherwise the UPDATE
   fails outright with "String or binary data would be truncated". The widen
   keeps the column's existing type (nvarchar or varchar) and nullability, and
   is skipped when the column is already wide enough or is already (MAX).

   Note: Bouvet Island (BV) and Heard Island and McDonald Islands (HM) are
   uninhabited and carry no zone in tzdata; they are given UTC and
   Indian/Kerguelen (the nearest zone, UTC+05) respectively.
   ============================================================================= */

SET NOCOUNT ON;

/* ---------------------------------------------------------------------------
   1. Widen dbo.Country.TimeZone if it cannot hold 600 characters.
   --------------------------------------------------------------------------- */

DECLARE @widen nvarchar(400);

SELECT  @widen = CONCAT(N'ALTER TABLE dbo.Country ALTER COLUMN TimeZone ',
                        t.name, N'(1000) ',
                        CASE WHEN c.is_nullable = 1 THEN N'NULL' ELSE N'NOT NULL' END,
                        N';')
FROM    sys.columns AS c
        INNER JOIN sys.types AS t
            ON t.user_type_id = c.user_type_id
WHERE   c.object_id = OBJECT_ID(N'dbo.Country')
        AND c.name = N'TimeZone'
        AND t.name IN (N'nvarchar', N'varchar')
        AND c.max_length <> -1   -- -1 is (MAX): already big enough
        AND c.max_length / CASE WHEN t.name = N'nvarchar' THEN 2 ELSE 1 END < 600;

IF @widen IS NOT NULL
BEGIN
    PRINT CONCAT(N'Widening column: ', @widen);
    EXEC sys.sp_executesql @widen;
END
ELSE
    PRINT N'Column width is already sufficient; no ALTER needed.';

/* ---------------------------------------------------------------------------
   2. Populate the time zones.
   --------------------------------------------------------------------------- */

UPDATE  c
SET     c.TimeZone = v.TimeZone
FROM    dbo.Country AS c
        INNER JOIN (VALUES
    (N'AF', N'Asia/Kabul'),                                                   -- 1 Afghanistan
    (N'AL', N'Europe/Tirane'),                                                -- 2 Albania
    (N'DZ', N'Africa/Algiers'),                                               -- 3 Algeria
    (N'AS', N'Pacific/Pago_Pago'),                                            -- 4 American Samoa
    (N'AD', N'Europe/Andorra'),                                               -- 5 Andorra
    (N'AO', N'Africa/Luanda'),                                                -- 6 Angola
    (N'AI', N'America/Anguilla'),                                             -- 7 Anguilla
    (N'AQ', N'Antarctica/McMurdo,Antarctica/Casey,Antarctica/Davis,Antarctica/DumontDUrville,Antarctica/Mawson,Antarctica/Palmer,Antarctica/Rothera,Antarctica/Syowa,Antarctica/Troll,Antarctica/Vostok'), -- 8 Antarctica
    (N'AG', N'America/Antigua'),                                              -- 9 Antigua and Barbuda
    (N'AR', N'America/Argentina/Buenos_Aires,America/Argentina/Cordoba,America/Argentina/Salta,America/Argentina/Jujuy,America/Argentina/Tucuman,America/Argentina/Catamarca,America/Argentina/La_Rioja,America/Argentina/San_Juan,America/Argentina/Mendoza,America/Argentina/San_Luis,America/Argentina/Rio_Gallegos,America/Argentina/Ushuaia'), -- 10 Argentina
    (N'AM', N'Asia/Yerevan'),                                                 -- 11 Armenia
    (N'AW', N'America/Aruba'),                                                -- 12 Aruba
    (N'AU', N'Australia/Sydney,Australia/Lord_Howe,Antarctica/Macquarie,Australia/Hobart,Australia/Melbourne,Australia/Broken_Hill,Australia/Brisbane,Australia/Lindeman,Australia/Adelaide,Australia/Darwin,Australia/Perth,Australia/Eucla'), -- 13 Australia
    (N'AT', N'Europe/Vienna'),                                                -- 14 Austria
    (N'AZ', N'Asia/Baku'),                                                    -- 15 Azerbaijan
    (N'BS', N'America/Nassau'),                                               -- 16 Bahamas (the)
    (N'BH', N'Asia/Bahrain'),                                                 -- 17 Bahrain
    (N'BD', N'Asia/Dhaka'),                                                   -- 18 Bangladesh
    (N'BB', N'America/Barbados'),                                             -- 19 Barbados
    (N'BY', N'Europe/Minsk'),                                                 -- 20 Belarus
    (N'BE', N'Europe/Brussels'),                                              -- 21 Belgium
    (N'BZ', N'America/Belize'),                                               -- 22 Belize
    (N'BJ', N'Africa/Porto-Novo'),                                            -- 23 Benin
    (N'BM', N'Atlantic/Bermuda'),                                             -- 24 Bermuda
    (N'BT', N'Asia/Thimphu'),                                                 -- 25 Bhutan
    (N'BO', N'America/La_Paz'),                                               -- 26 Bolivia (Plurinational State of)
    (N'BQ', N'America/Kralendijk'),                                           -- 27 Bonaire, Sint Eustatius and Saba
    (N'BA', N'Europe/Sarajevo'),                                              -- 28 Bosnia and Herzegovina
    (N'BW', N'Africa/Gaborone'),                                              -- 29 Botswana
    (N'BV', N'UTC'),                                                          -- 30 Bouvet Island
    (N'BR', N'America/Sao_Paulo,America/Noronha,America/Belem,America/Fortaleza,America/Recife,America/Araguaina,America/Maceio,America/Bahia,America/Campo_Grande,America/Cuiaba,America/Santarem,America/Porto_Velho,America/Boa_Vista,America/Manaus,America/Eirunepe,America/Rio_Branco'), -- 31 Brazil
    (N'IO', N'Indian/Chagos'),                                                -- 32 British Indian Ocean Territory (the)
    (N'BN', N'Asia/Brunei'),                                                  -- 33 Brunei Darussalam
    (N'BG', N'Europe/Sofia'),                                                 -- 34 Bulgaria
    (N'BF', N'Africa/Ouagadougou'),                                           -- 35 Burkina Faso
    (N'BI', N'Africa/Bujumbura'),                                             -- 36 Burundi
    (N'CV', N'Atlantic/Cape_Verde'),                                          -- 37 Cabo Verde
    (N'KH', N'Asia/Phnom_Penh'),                                              -- 38 Cambodia
    (N'CM', N'Africa/Douala'),                                                -- 39 Cameroon
    (N'CA', N'America/Toronto,America/St_Johns,America/Halifax,America/Glace_Bay,America/Moncton,America/Goose_Bay,America/Blanc-Sablon,America/Iqaluit,America/Atikokan,America/Winnipeg,America/Resolute,America/Rankin_Inlet,America/Regina,America/Swift_Current,America/Edmonton,America/Cambridge_Bay,America/Inuvik,America/Vancouver,America/Creston,America/Dawson_Creek,America/Fort_Nelson,America/Whitehorse,America/Dawson'), -- 40 Canada
    (N'KY', N'America/Cayman'),                                               -- 41 Cayman Islands (the)
    (N'CF', N'Africa/Bangui'),                                                -- 42 Central African Republic (the)
    (N'TD', N'Africa/Ndjamena'),                                              -- 43 Chad
    (N'CL', N'America/Santiago,America/Coyhaique,America/Punta_Arenas,Pacific/Easter'), -- 44 Chile
    (N'CN', N'Asia/Shanghai,Asia/Urumqi'),                                    -- 45 China
    (N'CX', N'Indian/Christmas'),                                             -- 46 Christmas Island
    (N'CC', N'Indian/Cocos'),                                                 -- 47 Cocos (Keeling) Islands (the)
    (N'CO', N'America/Bogota'),                                               -- 48 Colombia
    (N'KM', N'Indian/Comoro'),                                                -- 49 Comoros (the)
    (N'CD', N'Africa/Kinshasa,Africa/Lubumbashi'),                            -- 50 Congo (the Democratic Republic of the)
    (N'CG', N'Africa/Brazzaville'),                                           -- 51 Congo (the)
    (N'CK', N'Pacific/Rarotonga'),                                            -- 52 Cook Islands (the)
    (N'CR', N'America/Costa_Rica'),                                           -- 53 Costa Rica
    (N'HR', N'Europe/Zagreb'),                                                -- 54 Croatia
    (N'CU', N'America/Havana'),                                               -- 55 Cuba
    (N'CW', N'America/Curacao'),                                              -- 56 Curacao
    (N'CY', N'Asia/Nicosia,Asia/Famagusta'),                                  -- 57 Cyprus
    (N'CZ', N'Europe/Prague'),                                                -- 58 Czechia
    (N'CI', N'Africa/Abidjan'),                                               -- 59 Cote d'Ivoire
    (N'DK', N'Europe/Copenhagen'),                                            -- 60 Denmark
    (N'DJ', N'Africa/Djibouti'),                                              -- 61 Djibouti
    (N'DM', N'America/Dominica'),                                             -- 62 Dominica
    (N'DO', N'America/Santo_Domingo'),                                        -- 63 Dominican Republic (the)
    (N'EC', N'America/Guayaquil,Pacific/Galapagos'),                          -- 64 Ecuador
    (N'EG', N'Africa/Cairo'),                                                 -- 65 Egypt
    (N'SV', N'America/El_Salvador'),                                          -- 66 El Salvador
    (N'GQ', N'Africa/Malabo'),                                                -- 67 Equatorial Guinea
    (N'ER', N'Africa/Asmara'),                                                -- 68 Eritrea
    (N'EE', N'Europe/Tallinn'),                                               -- 69 Estonia
    (N'SZ', N'Africa/Mbabane'),                                               -- 70 Eswatini
    (N'ET', N'Africa/Addis_Ababa'),                                           -- 71 Ethiopia
    (N'FK', N'Atlantic/Stanley'),                                             -- 72 Falkland Islands (the) [Malvinas]
    (N'FO', N'Atlantic/Faroe'),                                               -- 73 Faroe Islands (the)
    (N'FJ', N'Pacific/Fiji'),                                                 -- 74 Fiji
    (N'FI', N'Europe/Helsinki'),                                              -- 75 Finland
    (N'FR', N'Europe/Paris'),                                                 -- 76 France
    (N'GF', N'America/Cayenne'),                                              -- 77 French Guiana
    (N'PF', N'Pacific/Tahiti,Pacific/Marquesas,Pacific/Gambier'),             -- 78 French Polynesia
    (N'TF', N'Indian/Kerguelen'),                                             -- 79 French Southern Territories (the)
    (N'GA', N'Africa/Libreville'),                                            -- 80 Gabon
    (N'GM', N'Africa/Banjul'),                                                -- 81 Gambia (the)
    (N'GE', N'Asia/Tbilisi'),                                                 -- 82 Georgia
    (N'DE', N'Europe/Berlin,Europe/Busingen'),                                -- 83 Germany
    (N'GH', N'Africa/Accra'),                                                 -- 84 Ghana
    (N'GI', N'Europe/Gibraltar'),                                             -- 85 Gibraltar
    (N'GR', N'Europe/Athens'),                                                -- 86 Greece
    (N'GL', N'America/Nuuk,America/Danmarkshavn,America/Scoresbysund,America/Thule'), -- 87 Greenland
    (N'GD', N'America/Grenada'),                                              -- 88 Grenada
    (N'GP', N'America/Guadeloupe'),                                           -- 89 Guadeloupe
    (N'GU', N'Pacific/Guam'),                                                 -- 90 Guam
    (N'GT', N'America/Guatemala'),                                            -- 91 Guatemala
    (N'GG', N'Europe/Guernsey'),                                              -- 92 Guernsey
    (N'GN', N'Africa/Conakry'),                                               -- 93 Guinea
    (N'GW', N'Africa/Bissau'),                                                -- 94 Guinea-Bissau
    (N'GY', N'America/Guyana'),                                               -- 95 Guyana
    (N'HT', N'America/Port-au-Prince'),                                       -- 96 Haiti
    (N'HM', N'Indian/Kerguelen'),                                             -- 97 Heard Island and McDonald Islands
    (N'VA', N'Europe/Vatican'),                                               -- 98 Holy See (the)
    (N'HN', N'America/Tegucigalpa'),                                          -- 99 Honduras
    (N'HK', N'Asia/Hong_Kong'),                                               -- 100 Hong Kong
    (N'HU', N'Europe/Budapest'),                                              -- 101 Hungary
    (N'IS', N'Atlantic/Reykjavik'),                                           -- 102 Iceland
    (N'IN', N'Asia/Kolkata'),                                                 -- 103 India
    (N'ID', N'Asia/Jakarta,Asia/Pontianak,Asia/Makassar,Asia/Jayapura'),      -- 104 Indonesia
    (N'IR', N'Asia/Tehran'),                                                  -- 105 Iran (Islamic Republic of)
    (N'IQ', N'Asia/Baghdad'),                                                 -- 106 Iraq
    (N'IE', N'Europe/Dublin'),                                                -- 107 Ireland
    (N'IM', N'Europe/Isle_of_Man'),                                           -- 108 Isle of Man
    (N'IL', N'Asia/Jerusalem'),                                               -- 109 Israel
    (N'IT', N'Europe/Rome'),                                                  -- 110 Italy
    (N'JM', N'America/Jamaica'),                                              -- 111 Jamaica
    (N'JP', N'Asia/Tokyo'),                                                   -- 112 Japan
    (N'JE', N'Europe/Jersey'),                                                -- 113 Jersey
    (N'JO', N'Asia/Amman'),                                                   -- 114 Jordan
    (N'KZ', N'Asia/Almaty,Asia/Qyzylorda,Asia/Qostanay,Asia/Aqtobe,Asia/Aqtau,Asia/Atyrau,Asia/Oral'), -- 115 Kazakhstan
    (N'KE', N'Africa/Nairobi'),                                               -- 116 Kenya
    (N'KI', N'Pacific/Tarawa,Pacific/Kanton,Pacific/Kiritimati'),             -- 117 Kiribati
    (N'KP', N'Asia/Pyongyang'),                                               -- 118 Korea (the Democratic People's Republic of)
    (N'KR', N'Asia/Seoul'),                                                   -- 119 Korea (the Republic of)
    (N'KW', N'Asia/Kuwait'),                                                  -- 120 Kuwait
    (N'KG', N'Asia/Bishkek'),                                                 -- 121 Kyrgyzstan
    (N'LA', N'Asia/Vientiane'),                                               -- 122 Lao People's Democratic Republic (the)
    (N'LV', N'Europe/Riga'),                                                  -- 123 Latvia
    (N'LB', N'Asia/Beirut'),                                                  -- 124 Lebanon
    (N'LS', N'Africa/Maseru'),                                                -- 125 Lesotho
    (N'LR', N'Africa/Monrovia'),                                              -- 126 Liberia
    (N'LY', N'Africa/Tripoli'),                                               -- 127 Libya
    (N'LI', N'Europe/Vaduz'),                                                 -- 128 Liechtenstein
    (N'LT', N'Europe/Vilnius'),                                               -- 129 Lithuania
    (N'LU', N'Europe/Luxembourg'),                                            -- 130 Luxembourg
    (N'MO', N'Asia/Macau'),                                                   -- 131 Macao
    (N'MG', N'Indian/Antananarivo'),                                          -- 132 Madagascar
    (N'MW', N'Africa/Blantyre'),                                              -- 133 Malawi
    (N'MY', N'Asia/Kuala_Lumpur,Asia/Kuching'),                               -- 134 Malaysia
    (N'MV', N'Indian/Maldives'),                                              -- 135 Maldives
    (N'ML', N'Africa/Bamako'),                                                -- 136 Mali
    (N'MT', N'Europe/Malta'),                                                 -- 137 Malta
    (N'MH', N'Pacific/Majuro,Pacific/Kwajalein'),                             -- 138 Marshall Islands (the)
    (N'MQ', N'America/Martinique'),                                           -- 139 Martinique
    (N'MR', N'Africa/Nouakchott'),                                            -- 140 Mauritania
    (N'MU', N'Indian/Mauritius'),                                             -- 141 Mauritius
    (N'YT', N'Indian/Mayotte'),                                               -- 142 Mayotte
    (N'MX', N'America/Mexico_City,America/Cancun,America/Merida,America/Monterrey,America/Matamoros,America/Chihuahua,America/Ciudad_Juarez,America/Ojinaga,America/Mazatlan,America/Bahia_Banderas,America/Hermosillo,America/Tijuana'), -- 143 Mexico
    (N'FM', N'Pacific/Pohnpei,Pacific/Chuuk,Pacific/Kosrae'),                 -- 144 Micronesia (Federated States of)
    (N'MD', N'Europe/Chisinau'),                                              -- 145 Moldova (the Republic of)
    (N'MC', N'Europe/Monaco'),                                                -- 146 Monaco
    (N'MN', N'Asia/Ulaanbaatar,Asia/Hovd'),                                   -- 147 Mongolia
    (N'ME', N'Europe/Podgorica'),                                             -- 148 Montenegro
    (N'MS', N'America/Montserrat'),                                           -- 149 Montserrat
    (N'MA', N'Africa/Casablanca'),                                            -- 150 Morocco
    (N'MZ', N'Africa/Maputo'),                                                -- 151 Mozambique
    (N'MM', N'Asia/Yangon'),                                                  -- 152 Myanmar
    (N'NA', N'Africa/Windhoek'),                                              -- 153 Namibia
    (N'NR', N'Pacific/Nauru'),                                                -- 154 Nauru
    (N'NP', N'Asia/Kathmandu'),                                               -- 155 Nepal
    (N'NL', N'Europe/Amsterdam'),                                             -- 156 Netherlands (the)
    (N'NC', N'Pacific/Noumea'),                                               -- 157 New Caledonia
    (N'NZ', N'Pacific/Auckland,Pacific/Chatham'),                             -- 158 New Zealand
    (N'NI', N'America/Managua'),                                              -- 159 Nicaragua
    (N'NE', N'Africa/Niamey'),                                                -- 160 Niger (the)
    (N'NG', N'Africa/Lagos'),                                                 -- 161 Nigeria
    (N'NU', N'Pacific/Niue'),                                                 -- 162 Niue
    (N'NF', N'Pacific/Norfolk'),                                              -- 163 Norfolk Island
    (N'MP', N'Pacific/Saipan'),                                               -- 164 Northern Mariana Islands (the)
    (N'NO', N'Europe/Oslo'),                                                  -- 165 Norway
    (N'OM', N'Asia/Muscat'),                                                  -- 166 Oman
    (N'PK', N'Asia/Karachi'),                                                 -- 167 Pakistan
    (N'PW', N'Pacific/Palau'),                                                -- 168 Palau
    (N'PS', N'Asia/Gaza,Asia/Hebron'),                                        -- 169 Palestine, State of
    (N'PA', N'America/Panama'),                                               -- 170 Panama
    (N'PG', N'Pacific/Port_Moresby,Pacific/Bougainville'),                    -- 171 Papua New Guinea
    (N'PY', N'America/Asuncion'),                                             -- 172 Paraguay
    (N'PE', N'America/Lima'),                                                 -- 173 Peru
    (N'PH', N'Asia/Manila'),                                                  -- 174 Philippines (the)
    (N'PN', N'Pacific/Pitcairn'),                                             -- 175 Pitcairn
    (N'PL', N'Europe/Warsaw'),                                                -- 176 Poland
    (N'PT', N'Europe/Lisbon,Atlantic/Madeira,Atlantic/Azores'),               -- 177 Portugal
    (N'PR', N'America/Puerto_Rico'),                                          -- 178 Puerto Rico
    (N'QA', N'Asia/Qatar'),                                                   -- 179 Qatar
    (N'MK', N'Europe/Skopje'),                                                -- 180 Republic of North Macedonia
    (N'RO', N'Europe/Bucharest'),                                             -- 181 Romania
    (N'RU', N'Europe/Moscow,Europe/Kaliningrad,Europe/Kirov,Europe/Volgograd,Europe/Astrakhan,Europe/Saratov,Europe/Ulyanovsk,Europe/Samara,Asia/Yekaterinburg,Asia/Omsk,Asia/Novosibirsk,Asia/Barnaul,Asia/Tomsk,Asia/Novokuznetsk,Asia/Krasnoyarsk,Asia/Irkutsk,Asia/Chita,Asia/Yakutsk,Asia/Khandyga,Asia/Vladivostok,Asia/Ust-Nera,Asia/Magadan,Asia/Sakhalin,Asia/Srednekolymsk,Asia/Kamchatka,Asia/Anadyr'), -- 182 Russian Federation (the)
    (N'RW', N'Africa/Kigali'),                                                -- 183 Rwanda
    (N'RE', N'Indian/Reunion'),                                               -- 184 Reunion
    (N'BL', N'America/St_Barthelemy'),                                        -- 185 Saint Barthelemy
    (N'SH', N'Atlantic/St_Helena'),                                           -- 186 Saint Helena, Ascension and Tristan da Cunha
    (N'KN', N'America/St_Kitts'),                                             -- 187 Saint Kitts and Nevis
    (N'LC', N'America/St_Lucia'),                                             -- 188 Saint Lucia
    (N'MF', N'America/Marigot'),                                              -- 189 Saint Martin (French part)
    (N'PM', N'America/Miquelon'),                                             -- 190 Saint Pierre and Miquelon
    (N'VC', N'America/St_Vincent'),                                           -- 191 Saint Vincent and the Grenadines
    (N'WS', N'Pacific/Apia'),                                                 -- 192 Samoa
    (N'SM', N'Europe/San_Marino'),                                            -- 193 San Marino
    (N'ST', N'Africa/Sao_Tome'),                                              -- 194 Sao Tome and Principe
    (N'SA', N'Asia/Riyadh'),                                                  -- 195 Saudi Arabia
    (N'SN', N'Africa/Dakar'),                                                 -- 196 Senegal
    (N'RS', N'Europe/Belgrade'),                                              -- 197 Serbia
    (N'SC', N'Indian/Mahe'),                                                  -- 198 Seychelles
    (N'SL', N'Africa/Freetown'),                                              -- 199 Sierra Leone
    (N'SG', N'Asia/Singapore'),                                               -- 200 Singapore
    (N'SX', N'America/Lower_Princes'),                                        -- 201 Sint Maarten (Dutch part)
    (N'SK', N'Europe/Bratislava'),                                            -- 202 Slovakia
    (N'SI', N'Europe/Ljubljana'),                                             -- 203 Slovenia
    (N'SB', N'Pacific/Guadalcanal'),                                          -- 204 Solomon Islands
    (N'SO', N'Africa/Mogadishu'),                                             -- 205 Somalia
    (N'ZA', N'Africa/Johannesburg'),                                          -- 206 South Africa
    (N'GS', N'Atlantic/South_Georgia'),                                       -- 207 South Georgia and the South Sandwich Islands
    (N'SS', N'Africa/Juba'),                                                  -- 208 South Sudan
    (N'ES', N'Europe/Madrid,Africa/Ceuta,Atlantic/Canary'),                   -- 209 Spain
    (N'LK', N'Asia/Colombo'),                                                 -- 210 Sri Lanka
    (N'SD', N'Africa/Khartoum'),                                              -- 211 Sudan (the)
    (N'SR', N'America/Paramaribo'),                                           -- 212 Suriname
    (N'SJ', N'Arctic/Longyearbyen'),                                          -- 213 Svalbard and Jan Mayen
    (N'SE', N'Europe/Stockholm'),                                             -- 214 Sweden
    (N'CH', N'Europe/Zurich'),                                                -- 215 Switzerland
    (N'SY', N'Asia/Damascus'),                                                -- 216 Syrian Arab Republic
    (N'TW', N'Asia/Taipei'),                                                  -- 217 Taiwan (Province of China)
    (N'TJ', N'Asia/Dushanbe'),                                                -- 218 Tajikistan
    (N'TZ', N'Africa/Dar_es_Salaam'),                                         -- 219 Tanzania, United Republic of
    (N'TH', N'Asia/Bangkok'),                                                 -- 220 Thailand
    (N'TL', N'Asia/Dili'),                                                    -- 221 Timor-Leste
    (N'TG', N'Africa/Lome'),                                                  -- 222 Togo
    (N'TK', N'Pacific/Fakaofo'),                                              -- 223 Tokelau
    (N'TO', N'Pacific/Tongatapu'),                                            -- 224 Tonga
    (N'TT', N'America/Port_of_Spain'),                                        -- 225 Trinidad and Tobago
    (N'TN', N'Africa/Tunis'),                                                 -- 226 Tunisia
    (N'TR', N'Europe/Istanbul'),                                              -- 227 Turkey
    (N'TM', N'Asia/Ashgabat'),                                                -- 228 Turkmenistan
    (N'TC', N'America/Grand_Turk'),                                           -- 229 Turks and Caicos Islands (the)
    (N'TV', N'Pacific/Funafuti'),                                             -- 230 Tuvalu
    (N'UG', N'Africa/Kampala'),                                               -- 231 Uganda
    (N'UA', N'Europe/Kyiv,Europe/Simferopol'),                                -- 232 Ukraine
    (N'AE', N'Asia/Dubai'),                                                   -- 233 United Arab Emirates (the)
    (N'GB', N'Europe/London'),                                                -- 234 United Kingdom of Great Britain and Northern Ireland (the)
    (N'UM', N'Pacific/Midway,Pacific/Wake'),                                  -- 235 United States Minor Outlying Islands (the)
    (N'US', N'America/New_York,America/Detroit,America/Kentucky/Louisville,America/Kentucky/Monticello,America/Indiana/Indianapolis,America/Indiana/Vincennes,America/Indiana/Winamac,America/Indiana/Marengo,America/Indiana/Petersburg,America/Indiana/Vevay,America/Chicago,America/Indiana/Tell_City,America/Indiana/Knox,America/Menominee,America/North_Dakota/Center,America/North_Dakota/New_Salem,America/North_Dakota/Beulah,America/Denver,America/Boise,America/Phoenix,America/Los_Angeles,America/Anchorage,America/Juneau,America/Sitka,America/Metlakatla,America/Yakutat,America/Nome,America/Adak,Pacific/Honolulu'), -- 236 United States of America (the)
    (N'UY', N'America/Montevideo'),                                           -- 237 Uruguay
    (N'UZ', N'Asia/Tashkent,Asia/Samarkand'),                                 -- 238 Uzbekistan
    (N'VU', N'Pacific/Efate'),                                                -- 239 Vanuatu
    (N'VE', N'America/Caracas'),                                              -- 240 Venezuela (Bolivarian Republic of)
    (N'VN', N'Asia/Ho_Chi_Minh'),                                             -- 241 Viet Nam
    (N'VG', N'America/Tortola'),                                              -- 242 Virgin Islands (British)
    (N'VI', N'America/St_Thomas'),                                            -- 243 Virgin Islands (U.S.)
    (N'WF', N'Pacific/Wallis'),                                               -- 244 Wallis and Futuna
    (N'EH', N'Africa/El_Aaiun'),                                              -- 245 Western Sahara
    (N'YE', N'Asia/Aden'),                                                    -- 246 Yemen
    (N'ZM', N'Africa/Lusaka'),                                                -- 247 Zambia
    (N'ZW', N'Africa/Harare'),                                                -- 248 Zimbabwe
    (N'AX', N'Europe/Mariehamn')                                              -- 249 Aland Islands
        ) AS v (Code, TimeZone)
            ON v.Code = c.Code;

PRINT CONCAT(N'Countries updated: ', @@ROWCOUNT);

/* ---------------------------------------------------------------------------
   3. Anything left without a time zone? (expected: 0 rows)
   --------------------------------------------------------------------------- */
SELECT  c.ID, c.Name, c.Code
FROM    dbo.Country AS c
WHERE   c.TimeZone IS NULL
        OR LTRIM(RTRIM(c.TimeZone)) = N'';
