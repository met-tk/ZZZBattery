package Ym;

import kotlin.enums.EnumEntries;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;

/* JADX WARN: Failed to restore enum class, 'enum' modifier and super class removed */
/* JADX WARN: Unknown enum class pattern. Please report as an issue! */
/* renamed from: Ym.b, reason: case insensitive filesystem */
/* loaded from: X:\AI project\解包\tools\classes2.dex */
public final class EnumC0868b {
    private static final /* synthetic */ EnumEntries $ENTRIES;
    private static final /* synthetic */ EnumC0868b[] $VALUES;

    @G50.l
    public static final a Companion;

    @G50.l
    private String bbs;

    @G50.l
    private final String bbs_cdn;

    @G50.l
    private final String bbs_event;

    @G50.l
    private final String coverageReport;

    @G50.l
    private final String customer;

    @G50.l
    private final String envName;
    private final int index;

    @G50.l
    private final String login;

    @G50.l
    private final String record;

    @G50.l
    private String rnOffline;

    @G50.l
    private final String sdk;

    @G50.l
    private final String takumi;

    @G50.l
    private final String teamBuild;
    public static final EnumC0868b ONLINE = new EnumC0868b("ONLINE", 0, 2, "https://webapi.account.miyoushe.com", A.g, "https://api-takumi.miyoushe.com", "https://bbs-api-static.miyoushe.com", "https://api-sdk.mihoyo.com", "https://api-takumi-record.mihoyo.com", "https://api-takumi.mihoyo.com", "https://api-takumi.mihoyo.com", "production", "https://test-platqa-common.mihoyo.com", "https://bbs-api-tool.miyoushe.com", A.g);
    public static final EnumC0868b DEV = new EnumC0868b("DEV", 1, 5, "https://test.account.miyoushe.com", "https://dev-api.miyoushe.com", "https://devapi-takumi.miyoushe.com", "https://dev-api.miyoushe.com", "https://devapi-takumi.mihoyo.com", "https://dev-api.mihoyo.com", "https://ptsapi-takumi.mihoyo.com", "https://dev-api.miyoushe.com", "development", "https://test-platqa-common.mihoyo.com", "https://dev-api.mihoyo.com", "https://dev-api.miyoushe.com");
    public static final EnumC0868b QA = new EnumC0868b("QA", 2, 0, "https://test.account.miyoushe.com", "https://devapi-takumi.miyoushe.com", "https://devapi-takumi.miyoushe.com", "https://devapi-static.miyoushe.com/takumi", "https://devapi-takumi.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://ptsapi-takumi.mihoyo.com", "https://devapi-takumi.miyoushe.com", "testing", "https://test-platqa-common.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://devapi-takumi.miyoushe.com");
    public static final EnumC0868b SANDBOX = new EnumC0868b("SANDBOX", 3, 4, "https://test.account.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com", "https://ptsapi-takumi.mihoyo.com", "https://sandbox-api.mihoyo.com", "sandbox", "https://test-platqa-common.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com");
    public static final EnumC0868b BETA = new EnumC0868b("BETA", 4, 6, "https://pre-webapi.account.miyoushe.com", "https://bbs-api-beta.miyoushe.com", "https://preapi-takumi.miyoushe.com", "https://bbs-api-beta-static.miyoushe.com", "https://api-sdk.mihoyo.com", "https://preapi-takumi.mihoyo.com", "https://api-takumi.mihoyo.com", "https://preapi-takumi.mihoyo.com", "beta", "https://test-platqa-common.mihoyo.com", "https://bbs-api-tool-beta.miyoushe.com", "https://preapi-takumi.mihoyo.com");
    public static final EnumC0868b QA_A = new EnumC0868b("QA_A", 5, 7, "https://test.account.miyoushe.com/", "https://bbs-api-testing.mihoyo.com", "https://bbs-api-testing.mihoyo.com", "https://dev-api-a.miyoushe.com/", "https://devapi-takumi.mihoyo.com/", "https://devapi-takumi.mihoyo.com/", "https://ptsapi-takumi.mihoyo.com", "https://devapi-takumi.miyoushe.com", "testing3", "https://test-platqa-common.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://bbs-api-testing.mihoyo.com");
    public static final EnumC0868b PTS = new EnumC0868b("PTS", 6, 8, "https://test.account.miyoushe.com", "https://ptsapi-takumi.mihoyo.com", "https://devapi-takumi.miyoushe.com", "https://ptsapi-takumi.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://ptsapi-takumi.mihoyo.com", "https://ptsapi-takumi.mihoyo.com", "PTS", "https://test-platqa-common.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://ptsapi-takumi.mihoyo.com");
    public static final EnumC0868b GAME_SANDBOX = new EnumC0868b("GAME_SANDBOX", 7, 9, "https://sandbox-sdk.mihoyo.com", "https://ugc-api-testing-sandbox.mihoyo.com", "https://sandbox-sdk.mihoyo.com", "https://ugc-api-testing-sandbox.mihoyo.com", "https://sandbox-sdk.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com", "https://ptsapi-takumi.mihoyo.com", "https://sandbox-api.mihoyo.com", "game_sandbox", "https://test-platqa-common.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://ugc-api-testing-sandbox.mihoyo.com");
    public static final EnumC0868b GAME_BETA = new EnumC0868b("GAME_BETA", 8, 10, "https://sandbox-sdk.mihoyo.com", "https://ugc-api-testing-sandbox.mihoyo.com", "https://sandbox-sdk.mihoyo.com", "https://ugc-api-testing-sandbox.mihoyo.com", "https://sandbox-sdk.mihoyo.com", "https://sandbox-bbs-takumi.mihoyo.com", "https://ptsapi-takumi.mihoyo.com", "https://sandbox-api.mihoyo.com", "game_beta", "https://test-platqa-common.mihoyo.com", "https://devapi-takumi.mihoyo.com", "https://ugc-api-testing-sandbox.mihoyo.com");

    @r0({"SMAP\nApiEnvType.kt\nKotlin\n*S Kotlin\n*F\n+ 1 ApiEnvType.kt\ncom/mihoyo/hyperion/net/ApiEnvType$Companion\n+ 2 fake.kt\nkotlin/jvm/internal/FakeKt\n*L\n1#1,164:1\n1#2:165\n*E\n"})
    /* renamed from: Ym.b$a */
    /* loaded from: X:\AI project\解包\tools\classes2.dex */
    public static final class a {
        public /* synthetic */ a(DefaultConstructorMarker defaultConstructorMarker) {
            this();
        }

        @G50.l
        public final EnumC0868b a(@G50.l String str) {
            EnumC0868b enumC0868b;
            K.p(str, "s");
            EnumC0868b[] values = EnumC0868b.values();
            int length = values.length;
            int i = 0;
            while (true) {
                if (i >= length) {
                    enumC0868b = null;
                    break;
                }
                enumC0868b = values[i];
                if (K.g(enumC0868b.name(), str)) {
                    break;
                }
                i++;
            }
            return enumC0868b == null ? EnumC0868b.ONLINE : enumC0868b;
        }

        public a() {
        }
    }

    private static final /* synthetic */ EnumC0868b[] $values() {
        return new EnumC0868b[]{ONLINE, DEV, QA, SANDBOX, BETA, QA_A, PTS, GAME_SANDBOX, GAME_BETA};
    }

    static {
        EnumC0868b[] $values = $values();
        $VALUES = $values;
        $ENTRIES = Py.b.c($values);
        Companion = new a(null);
    }

    private EnumC0868b(String str, int i, int i2, String str2, String str3, String str4, String str5, String str6, String str7, String str8, String str9, String str10, String str11, String str12, String str13) {
        this.index = i2;
        this.login = str2;
        this.bbs = str3;
        this.takumi = str4;
        this.bbs_cdn = str5;
        this.sdk = str6;
        this.record = str7;
        this.customer = str8;
        this.bbs_event = str9;
        this.envName = str10;
        this.coverageReport = str11;
        this.teamBuild = str12;
        this.rnOffline = str13;
    }

    @G50.l
    public static EnumEntries<EnumC0868b> getEntries() {
        return $ENTRIES;
    }

    public static EnumC0868b valueOf(String str) {
        return (EnumC0868b) Enum.valueOf(EnumC0868b.class, str);
    }

    public static EnumC0868b[] values() {
        return (EnumC0868b[]) $VALUES.clone();
    }

    @G50.l
    public final String getBbs() {
        return this.bbs;
    }

    @G50.l
    public final String getBbs_cdn() {
        return this.bbs_cdn;
    }

    @G50.l
    public final String getBbs_event() {
        return this.bbs_event;
    }

    @G50.l
    public final String getCoverageReport() {
        return this.coverageReport;
    }

    @G50.l
    public final String getCustomer() {
        return this.customer;
    }

    @G50.l
    public final String getEnvName() {
        return this.envName;
    }

    public final int getIndex() {
        return this.index;
    }

    @G50.l
    public final String getLogin() {
        return this.login;
    }

    @G50.l
    public final String getRecord() {
        return this.record;
    }

    @G50.l
    public final String getRnOffline() {
        return this.rnOffline;
    }

    @G50.l
    public final String getSdk() {
        return this.sdk;
    }

    @G50.l
    public final String getTakumi() {
        return this.takumi;
    }

    @G50.l
    public final String getTeamBuild() {
        return this.teamBuild;
    }

    public final void setBbs(@G50.l String str) {
        K.p(str, "<set-?>");
        this.bbs = str;
    }

    public final void setRnOffline(@G50.l String str) {
        K.p(str, "<set-?>");
        this.rnOffline = str;
    }
}
