package Ym;

import kotlin.enums.EnumEntries;
import kotlin.jvm.internal.K;

@x0.v(parameters = 1)
/* loaded from: X:\AI project\解包\tools\classes2.dex */
public final class d {

    @G50.l
    public static final d a = new d();

    @G50.l
    public static final String b = "urlname";

    @G50.l
    public static final String c = "urlname:API_TAKUMI";

    @G50.l
    public static final String d = "urlname:API_BBS";

    @G50.l
    public static final String e = "urlname:API_LOGIN";

    @G50.l
    public static final String f = "urlname:API_BILIBILI";

    @G50.l
    public static final String g = "urlname:API_TAKUMI_CDN";

    @G50.l
    public static final String h = "urlname:API_SDK";

    @G50.l
    public static final String i = "urlname:API_RECORD";

    @G50.l
    public static final String j = "urlname:API_IPV6_TEST";

    @G50.l
    public static final String k = "urlname:API_CUSTOMER";

    @G50.l
    public static final String l = "urlname:API_BBS_EVENT";

    @G50.l
    public static final String m = "urlname:API_COVERAGE_REPORT";

    @G50.l
    public static final String n = "urlname:TEAM_BUILD";

    @G50.l
    public static final String o = "urlname:API_REACT_NATIVE_OFFLINE";
    public static final int p = 0;

    /* JADX WARN: Failed to restore enum class, 'enum' modifier and super class removed */
    /* JADX WARN: Unknown enum class pattern. Please report as an issue! */
    /* loaded from: X:\AI project\解包\tools\classes2.dex */
    public static final class a {
        private static final /* synthetic */ EnumEntries $ENTRIES;
        private static final /* synthetic */ a[] $VALUES;
        public static final a API_TAKUMI = new a("API_TAKUMI", 0);
        public static final a API_BBS = new a("API_BBS", 1);
        public static final a API_LOGIN = new a("API_LOGIN", 2);
        public static final a API_BILIBILI = new a("API_BILIBILI", 3);
        public static final a API_TAKUMI_CDN = new a("API_TAKUMI_CDN", 4);
        public static final a API_SDK = new a("API_SDK", 5);
        public static final a API_RECORD = new a("API_RECORD", 6);
        public static final a API_IPV6_TEST = new a("API_IPV6_TEST", 7);
        public static final a API_CUSTOMER = new a("API_CUSTOMER", 8);
        public static final a API_BBS_EVENT = new a("API_BBS_EVENT", 9);
        public static final a API_COVERAGE_REPORT = new a("API_COVERAGE_REPORT", 10);
        public static final a TEAM_BUILD = new a("TEAM_BUILD", 11);
        public static final a API_REACT_NATIVE_OFFLINE = new a("API_REACT_NATIVE_OFFLINE", 12);

        private static final /* synthetic */ a[] $values() {
            return new a[]{API_TAKUMI, API_BBS, API_LOGIN, API_BILIBILI, API_TAKUMI_CDN, API_SDK, API_RECORD, API_IPV6_TEST, API_CUSTOMER, API_BBS_EVENT, API_COVERAGE_REPORT, TEAM_BUILD, API_REACT_NATIVE_OFFLINE};
        }

        static {
            a[] $values = $values();
            $VALUES = $values;
            $ENTRIES = Py.b.c($values);
        }

        private a(String str, int i) {
        }

        @G50.l
        public static EnumEntries<a> getEntries() {
            return $ENTRIES;
        }

        public static a valueOf(String str) {
            return (a) Enum.valueOf(a.class, str);
        }

        public static a[] values() {
            return (a[]) $VALUES.clone();
        }
    }

    @G50.l
    public final a a(@G50.l String str) {
        K.p(str, "s");
        return a.valueOf(str);
    }
}
