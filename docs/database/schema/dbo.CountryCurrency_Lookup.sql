/*
    Table: dbo.CountryCurrency_Lookup
    Recorded: 2026-10-11 (created and populated by the database owner by hand)
    Revised:   2026-10-25 - table renamed from dbo.CountryCurrency by the database owner.

    One row per country, naming the currency that country uses and its
    symbol. 248 rows: every row of dbo.Country except Antarctica (ID 8,
    AQ), which has no currency of its own.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/CountryCurrency.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/CountryCurrencyConfiguration.cs

    Read by:
      ../procedures/dbo.spc_AddEditClientSetup.sql - derives a client's
        currency from its CountryID and stores the CurrencyID in
        dbo.TimesheetClientSetup.Currencycode.
      ../procedures/dbo.spc_GetClientListForMasterAdmin.sql - joins it back
        for CurrencyName and CurrencySymbol.
      src/ETimeSheet.Infrastructure/Repositories/MasterAdminRepository.cs
        (MasterAdminService checks a country has a currency before saving).

    Populated by:
      ../data/dbo.CountryCurrency_Lookup_Insert.sql - run by hand, once, after the
      table is created. The INSERT at the bottom of this file is the same
      248 rows, so the throwaway test database built from this folder
      holds them too.

    Column quirks worth knowing:
      - CurrencyID is IDENTITY(1,1) and the primary key. The rows are
        inserted with explicit ids (1 - 248, in dbo.Country ID order) so a
        CurrencyID means the same currency row in every database.
      - CountryID is dbo.Country.ID, but there is deliberately NO foreign
        key: nothing stops a row naming a country that does not exist.
      - A currency used by several countries has one row PER COUNTRY - Euro
        appears 36 times, each with its own CurrencyID. So CurrencyID
        identifies a country's currency, not a currency.
      - CurrencySymbol is nvarchar, not varchar: most symbols (₹, €, ₩,
        ر.س) are outside the ASCII range.
      - Symbols are NOT unique. "$" alone is shared by every dollar and
        several pesos, so show CurrencyName beside it, never the symbol by
        itself.

    Lengths below are characters.
*/

IF OBJECT_ID(N'dbo.CountryCurrency_Lookup', N'U') IS NOT NULL
    DROP TABLE dbo.CountryCurrency_Lookup;
GO

CREATE TABLE dbo.CountryCurrency_Lookup
(
    CurrencyID      int            IDENTITY(1,1) NOT NULL,
    CurrencySymbol  nvarchar(10)   NOT NULL,
    CurrencyName    nvarchar(100)  NOT NULL,
    CountryID       int            NOT NULL,
    CONSTRAINT PK_CountryCurrency PRIMARY KEY CLUSTERED (CurrencyID)
);
GO

-- The currency of every country in dbo.Country except Antarctica (ID 8).
-- Kept identical to ../data/dbo.CountryCurrency_Lookup_Insert.sql.
SET IDENTITY_INSERT dbo.CountryCurrency_Lookup ON;

INSERT INTO dbo.CountryCurrency_Lookup
    (CurrencyID, CurrencyName, CurrencySymbol, CountryID)
VALUES
    (  1, N'Afghan Afghani',                          N'؋',   1),
    (  2, N'Albanian Lek',                            N'L',   2),
    (  3, N'Algerian Dinar',                          N'د.ج',   3),
    (  4, N'United States Dollar',                    N'$',   4),
    (  5, N'Euro',                                    N'€',   5),
    (  6, N'Angolan Kwanza',                          N'Kz',   6),
    (  7, N'East Caribbean Dollar',                   N'$',   7),
    (  8, N'East Caribbean Dollar',                   N'$',   9),
    (  9, N'Argentine Peso',                          N'$',  10),
    ( 10, N'Armenian Dram',                           N'֏',  11),
    ( 11, N'Aruban Florin',                           N'ƒ',  12),
    ( 12, N'Australian Dollar',                       N'$',  13),
    ( 13, N'Euro',                                    N'€',  14),
    ( 14, N'Azerbaijani Manat',                       N'₼',  15),
    ( 15, N'Bahamian Dollar',                         N'$',  16),
    ( 16, N'Bahraini Dinar',                          N'.د.ب',  17),
    ( 17, N'Bangladeshi Taka',                        N'৳',  18),
    ( 18, N'Barbadian Dollar',                        N'$',  19),
    ( 19, N'Belarusian Ruble',                        N'Br',  20),
    ( 20, N'Euro',                                    N'€',  21),
    ( 21, N'Belize Dollar',                           N'$',  22),
    ( 22, N'West African CFA Franc',                  N'CFA',  23),
    ( 23, N'Bermudian Dollar',                        N'$',  24),
    ( 24, N'Bhutanese Ngultrum',                      N'Nu.',  25),
    ( 25, N'Bolivian Boliviano',                      N'Bs.',  26),
    ( 26, N'United States Dollar',                    N'$',  27),
    ( 27, N'Bosnia and Herzegovina Convertible Mark', N'KM',  28),
    ( 28, N'Botswana Pula',                           N'P',  29),
    ( 29, N'Norwegian Krone',                         N'kr',  30),
    ( 30, N'Brazilian Real',                          N'R$',  31),
    ( 31, N'United States Dollar',                    N'$',  32),
    ( 32, N'Brunei Dollar',                           N'$',  33),
    ( 33, N'Euro',                                    N'€',  34),
    ( 34, N'West African CFA Franc',                  N'CFA',  35),
    ( 35, N'Burundian Franc',                         N'FBu',  36),
    ( 36, N'Cape Verdean Escudo',                     N'Esc',  37),
    ( 37, N'Cambodian Riel',                          N'៛',  38),
    ( 38, N'Central African CFA Franc',               N'FCFA',  39),
    ( 39, N'Canadian Dollar',                         N'$',  40),
    ( 40, N'Cayman Islands Dollar',                   N'$',  41),
    ( 41, N'Central African CFA Franc',               N'FCFA',  42),
    ( 42, N'Central African CFA Franc',               N'FCFA',  43),
    ( 43, N'Chilean Peso',                            N'$',  44),
    ( 44, N'Chinese Yuan',                            N'¥',  45),
    ( 45, N'Australian Dollar',                       N'$',  46),
    ( 46, N'Australian Dollar',                       N'$',  47),
    ( 47, N'Colombian Peso',                          N'$',  48),
    ( 48, N'Comorian Franc',                          N'CF',  49),
    ( 49, N'Congolese Franc',                         N'FC',  50),
    ( 50, N'Central African CFA Franc',               N'FCFA',  51),
    ( 51, N'New Zealand Dollar',                      N'$',  52),
    ( 52, N'Costa Rican Colón',                       N'₡',  53),
    ( 53, N'Euro',                                    N'€',  54),
    ( 54, N'Cuban Peso',                              N'$',  55),
    ( 55, N'Caribbean Guilder',                       N'Cg',  56),
    ( 56, N'Euro',                                    N'€',  57),
    ( 57, N'Czech Koruna',                            N'Kč',  58),
    ( 58, N'West African CFA Franc',                  N'CFA',  59),
    ( 59, N'Danish Krone',                            N'kr',  60),
    ( 60, N'Djiboutian Franc',                        N'Fdj',  61),
    ( 61, N'East Caribbean Dollar',                   N'$',  62),
    ( 62, N'Dominican Peso',                          N'RD$',  63),
    ( 63, N'United States Dollar',                    N'$',  64),
    ( 64, N'Egyptian Pound',                          N'E£',  65),
    ( 65, N'United States Dollar',                    N'$',  66),
    ( 66, N'Central African CFA Franc',               N'FCFA',  67),
    ( 67, N'Eritrean Nakfa',                          N'Nfk',  68),
    ( 68, N'Euro',                                    N'€',  69),
    ( 69, N'Swazi Lilangeni',                         N'E',  70),
    ( 70, N'Ethiopian Birr',                          N'Br',  71),
    ( 71, N'Falkland Islands Pound',                  N'£',  72),
    ( 72, N'Danish Krone',                            N'kr',  73),
    ( 73, N'Fijian Dollar',                           N'$',  74),
    ( 74, N'Euro',                                    N'€',  75),
    ( 75, N'Euro',                                    N'€',  76),
    ( 76, N'Euro',                                    N'€',  77),
    ( 77, N'CFP Franc',                               N'₣',  78),
    ( 78, N'Euro',                                    N'€',  79),
    ( 79, N'Central African CFA Franc',               N'FCFA',  80),
    ( 80, N'Gambian Dalasi',                          N'D',  81),
    ( 81, N'Georgian Lari',                           N'₾',  82),
    ( 82, N'Euro',                                    N'€',  83),
    ( 83, N'Ghanaian Cedi',                           N'₵',  84),
    ( 84, N'Gibraltar Pound',                         N'£',  85),
    ( 85, N'Euro',                                    N'€',  86),
    ( 86, N'Danish Krone',                            N'kr',  87),
    ( 87, N'East Caribbean Dollar',                   N'$',  88),
    ( 88, N'Euro',                                    N'€',  89),
    ( 89, N'United States Dollar',                    N'$',  90),
    ( 90, N'Guatemalan Quetzal',                      N'Q',  91),
    ( 91, N'Pound Sterling',                          N'£',  92),
    ( 92, N'Guinean Franc',                           N'FG',  93),
    ( 93, N'West African CFA Franc',                  N'CFA',  94),
    ( 94, N'Guyanese Dollar',                         N'$',  95),
    ( 95, N'Haitian Gourde',                          N'G',  96),
    ( 96, N'Australian Dollar',                       N'$',  97),
    ( 97, N'Euro',                                    N'€',  98),
    ( 98, N'Honduran Lempira',                        N'L',  99),
    ( 99, N'Hong Kong Dollar',                        N'HK$', 100),
    (100, N'Hungarian Forint',                        N'Ft', 101),
    (101, N'Icelandic Króna',                         N'kr', 102),
    (102, N'Indian Rupee',                            N'₹', 103),
    (103, N'Indonesian Rupiah',                       N'Rp', 104),
    (104, N'Iranian Rial',                            N'﷼', 105),
    (105, N'Iraqi Dinar',                             N'ع.د', 106),
    (106, N'Euro',                                    N'€', 107),
    (107, N'Pound Sterling',                          N'£', 108),
    (108, N'Israeli New Shekel',                      N'₪', 109),
    (109, N'Euro',                                    N'€', 110),
    (110, N'Jamaican Dollar',                         N'J$', 111),
    (111, N'Japanese Yen',                            N'¥', 112),
    (112, N'Pound Sterling',                          N'£', 113),
    (113, N'Jordanian Dinar',                         N'د.ا', 114),
    (114, N'Kazakhstani Tenge',                       N'₸', 115),
    (115, N'Kenyan Shilling',                         N'KSh', 116),
    (116, N'Australian Dollar',                       N'$', 117),
    (117, N'North Korean Won',                        N'₩', 118),
    (118, N'South Korean Won',                        N'₩', 119),
    (119, N'Kuwaiti Dinar',                           N'د.ك', 120),
    (120, N'Kyrgyzstani Som',                         N'с', 121),
    (121, N'Lao Kip',                                 N'₭', 122),
    (122, N'Euro',                                    N'€', 123),
    (123, N'Lebanese Pound',                          N'ل.ل', 124),
    (124, N'Lesotho Loti',                            N'L', 125),
    (125, N'Liberian Dollar',                         N'$', 126),
    (126, N'Libyan Dinar',                            N'ل.د', 127),
    (127, N'Swiss Franc',                             N'CHF', 128),
    (128, N'Euro',                                    N'€', 129),
    (129, N'Euro',                                    N'€', 130),
    (130, N'Macanese Pataca',                         N'MOP$', 131),
    (131, N'Malagasy Ariary',                         N'Ar', 132),
    (132, N'Malawian Kwacha',                         N'MK', 133),
    (133, N'Malaysian Ringgit',                       N'RM', 134),
    (134, N'Maldivian Rufiyaa',                       N'Rf', 135),
    (135, N'West African CFA Franc',                  N'CFA', 136),
    (136, N'Euro',                                    N'€', 137),
    (137, N'United States Dollar',                    N'$', 138),
    (138, N'Euro',                                    N'€', 139),
    (139, N'Mauritanian Ouguiya',                     N'UM', 140),
    (140, N'Mauritian Rupee',                         N'₨', 141),
    (141, N'Euro',                                    N'€', 142),
    (142, N'Mexican Peso',                            N'$', 143),
    (143, N'United States Dollar',                    N'$', 144),
    (144, N'Moldovan Leu',                            N'L', 145),
    (145, N'Euro',                                    N'€', 146),
    (146, N'Mongolian Tögrög',                        N'₮', 147),
    (147, N'Euro',                                    N'€', 148),
    (148, N'East Caribbean Dollar',                   N'$', 149),
    (149, N'Moroccan Dirham',                         N'د.م.', 150),
    (150, N'Mozambican Metical',                      N'MT', 151),
    (151, N'Myanmar Kyat',                            N'K', 152),
    (152, N'Namibian Dollar',                         N'$', 153),
    (153, N'Australian Dollar',                       N'$', 154),
    (154, N'Nepalese Rupee',                          N'₨', 155),
    (155, N'Euro',                                    N'€', 156),
    (156, N'CFP Franc',                               N'₣', 157),
    (157, N'New Zealand Dollar',                      N'$', 158),
    (158, N'Nicaraguan Córdoba',                      N'C$', 159),
    (159, N'West African CFA Franc',                  N'CFA', 160),
    (160, N'Nigerian Naira',                          N'₦', 161),
    (161, N'New Zealand Dollar',                      N'$', 162),
    (162, N'Australian Dollar',                       N'$', 163),
    (163, N'United States Dollar',                    N'$', 164),
    (164, N'Norwegian Krone',                         N'kr', 165),
    (165, N'Omani Rial',                              N'ر.ع.', 166),
    (166, N'Pakistani Rupee',                         N'₨', 167),
    (167, N'United States Dollar',                    N'$', 168),
    (168, N'Israeli New Shekel',                      N'₪', 169),
    (169, N'Panamanian Balboa',                       N'B/.', 170),
    (170, N'Papua New Guinean Kina',                  N'K', 171),
    (171, N'Paraguayan Guaraní',                      N'₲', 172),
    (172, N'Peruvian Sol',                            N'S/', 173),
    (173, N'Philippine Peso',                         N'₱', 174),
    (174, N'New Zealand Dollar',                      N'$', 175),
    (175, N'Polish Złoty',                            N'zł', 176),
    (176, N'Euro',                                    N'€', 177),
    (177, N'United States Dollar',                    N'$', 178),
    (178, N'Qatari Riyal',                            N'ر.ق', 179),
    (179, N'Macedonian Denar',                        N'ден', 180),
    (180, N'Romanian Leu',                            N'lei', 181),
    (181, N'Russian Ruble',                           N'₽', 182),
    (182, N'Rwandan Franc',                           N'FRw', 183),
    (183, N'Euro',                                    N'€', 184),
    (184, N'Euro',                                    N'€', 185),
    (185, N'Saint Helena Pound',                      N'£', 186),
    (186, N'East Caribbean Dollar',                   N'$', 187),
    (187, N'East Caribbean Dollar',                   N'$', 188),
    (188, N'Euro',                                    N'€', 189),
    (189, N'Euro',                                    N'€', 190),
    (190, N'East Caribbean Dollar',                   N'$', 191),
    (191, N'Samoan Tālā',                             N'T', 192),
    (192, N'Euro',                                    N'€', 193),
    (193, N'São Tomé and Príncipe Dobra',             N'Db', 194),
    (194, N'Saudi Riyal',                             N'ر.س', 195),
    (195, N'West African CFA Franc',                  N'CFA', 196),
    (196, N'Serbian Dinar',                           N'дин.', 197),
    (197, N'Seychellois Rupee',                       N'₨', 198),
    (198, N'Sierra Leonean Leone',                    N'Le', 199),
    (199, N'Singapore Dollar',                        N'S$', 200),
    (200, N'Caribbean Guilder',                       N'Cg', 201),
    (201, N'Euro',                                    N'€', 202),
    (202, N'Euro',                                    N'€', 203),
    (203, N'Solomon Islands Dollar',                  N'$', 204),
    (204, N'Somali Shilling',                         N'Sh', 205),
    (205, N'South African Rand',                      N'R', 206),
    (206, N'Pound Sterling',                          N'£', 207),
    (207, N'South Sudanese Pound',                    N'£', 208),
    (208, N'Euro',                                    N'€', 209),
    (209, N'Sri Lankan Rupee',                        N'Rs', 210),
    (210, N'Sudanese Pound',                          N'ج.س.', 211),
    (211, N'Surinamese Dollar',                       N'$', 212),
    (212, N'Norwegian Krone',                         N'kr', 213),
    (213, N'Swedish Krona',                           N'kr', 214),
    (214, N'Swiss Franc',                             N'CHF', 215),
    (215, N'Syrian Pound',                            N'£S', 216),
    (216, N'New Taiwan Dollar',                       N'NT$', 217),
    (217, N'Tajikistani Somoni',                      N'SM', 218),
    (218, N'Tanzanian Shilling',                      N'TSh', 219),
    (219, N'Thai Baht',                               N'฿', 220),
    (220, N'United States Dollar',                    N'$', 221),
    (221, N'West African CFA Franc',                  N'CFA', 222),
    (222, N'New Zealand Dollar',                      N'$', 223),
    (223, N'Tongan Paʻanga',                          N'T$', 224),
    (224, N'Trinidad and Tobago Dollar',              N'TT$', 225),
    (225, N'Tunisian Dinar',                          N'د.ت', 226),
    (226, N'Turkish Lira',                            N'₺', 227),
    (227, N'Turkmenistani Manat',                     N'm', 228),
    (228, N'United States Dollar',                    N'$', 229),
    (229, N'Australian Dollar',                       N'$', 230),
    (230, N'Ugandan Shilling',                        N'USh', 231),
    (231, N'Ukrainian Hryvnia',                       N'₴', 232),
    (232, N'UAE Dirham',                              N'د.إ', 233),
    (233, N'Pound Sterling',                          N'£', 234),
    (234, N'United States Dollar',                    N'$', 235),
    (235, N'United States Dollar',                    N'$', 236),
    (236, N'Uruguayan Peso',                          N'$U', 237),
    (237, N'Uzbekistani Som',                         N'soʻm', 238),
    (238, N'Vanuatu Vatu',                            N'VT', 239),
    (239, N'Venezuelan Bolívar',                      N'Bs.', 240),
    (240, N'Vietnamese Đồng',                         N'₫', 241),
    (241, N'United States Dollar',                    N'$', 242),
    (242, N'United States Dollar',                    N'$', 243),
    (243, N'CFP Franc',                               N'₣', 244),
    (244, N'Moroccan Dirham',                         N'د.م.', 245),
    (245, N'Yemeni Rial',                             N'﷼', 246),
    (246, N'Zambian Kwacha',                          N'ZK', 247),
    (247, N'Zimbabwe Gold',                           N'ZiG', 248),
    (248, N'Euro',                                    N'€', 249);

SET IDENTITY_INSERT dbo.CountryCurrency_Lookup OFF;
GO
