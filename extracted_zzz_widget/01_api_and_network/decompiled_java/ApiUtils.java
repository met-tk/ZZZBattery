package Ym;

import Ym.d;
import android.net.Uri;
import java.util.HashSet;
import java.util.Iterator;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import pp.Y;
import yA.P;
import zy.J;

@x0.v(parameters = 0)
@r0({"SMAP\nApiUtils.kt\nKotlin\n*S Kotlin\n*F\n+ 1 ApiUtils.kt\ncom/mihoyo/hyperion/net/ApiUtils\n+ 2 ArraysJVM.kt\nkotlin/collections/ArraysKt__ArraysJVMKt\n+ 3 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,113:1\n37#2:114\n36#2,3:115\n1761#3,3:118\n*S KotlinDebug\n*F\n+ 1 ApiUtils.kt\ncom/mihoyo/hyperion/net/ApiUtils\n*L\n72#1:114\n72#1:115,3\n110#1:118,3\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes2.dex */
public final class e {

    @G50.l
    public static final String b = "http://api.bilibili.com/";

    @G50.l
    public static final String c = "https://bbs-api-v6.mihoyo.com/";

    @G50.m
    public static String[] e;

    @G50.l
    public static final e a = new e();

    @G50.l
    public static EnumC0868b d = EnumC0868b.ONLINE;
    public static final int f = 8;

    /* loaded from: X:\AI project\解包\tools\classes2.dex */
    public /* synthetic */ class a {
        public static final /* synthetic */ int[] a;

        static {
            int[] iArr = new int[d.a.values().length];
            try {
                iArr[d.a.API_BILIBILI.ordinal()] = 1;
            } catch (NoSuchFieldError unused) {
            }
            try {
                iArr[d.a.API_IPV6_TEST.ordinal()] = 2;
            } catch (NoSuchFieldError unused2) {
            }
            try {
                iArr[d.a.API_TAKUMI.ordinal()] = 3;
            } catch (NoSuchFieldError unused3) {
            }
            try {
                iArr[d.a.API_LOGIN.ordinal()] = 4;
            } catch (NoSuchFieldError unused4) {
            }
            try {
                iArr[d.a.API_TAKUMI_CDN.ordinal()] = 5;
            } catch (NoSuchFieldError unused5) {
            }
            try {
                iArr[d.a.API_SDK.ordinal()] = 6;
            } catch (NoSuchFieldError unused6) {
            }
            try {
                iArr[d.a.API_BBS.ordinal()] = 7;
            } catch (NoSuchFieldError unused7) {
            }
            try {
                iArr[d.a.API_RECORD.ordinal()] = 8;
            } catch (NoSuchFieldError unused8) {
            }
            try {
                iArr[d.a.API_CUSTOMER.ordinal()] = 9;
            } catch (NoSuchFieldError unused9) {
            }
            try {
                iArr[d.a.API_BBS_EVENT.ordinal()] = 10;
            } catch (NoSuchFieldError unused10) {
            }
            try {
                iArr[d.a.API_COVERAGE_REPORT.ordinal()] = 11;
            } catch (NoSuchFieldError unused11) {
            }
            try {
                iArr[d.a.TEAM_BUILD.ordinal()] = 12;
            } catch (NoSuchFieldError unused12) {
            }
            try {
                iArr[d.a.API_REACT_NATIVE_OFFLINE.ordinal()] = 13;
            } catch (NoSuchFieldError unused13) {
            }
            a = iArr;
        }
    }

    public final String[] a() {
        Uri parse;
        String host;
        HashSet hashSet = new HashSet();
        for (d.a aVar : d.a.values()) {
            if (aVar != d.a.API_BILIBILI && (parse = Uri.parse(b(aVar))) != null && (host = parse.getHost()) != null) {
                hashSet.add(host);
            }
        }
        return (String[]) hashSet.toArray(new String[0]);
    }

    @G50.l
    public final String b(@G50.l d.a aVar) {
        String str;
        K.p(aVar, "type");
        switch (a.a[aVar.ordinal()]) {
            case 1:
                str = b;
                break;
            case 2:
                str = c;
                break;
            case 3:
                str = d.getTakumi();
                break;
            case 4:
                str = d.getLogin();
                break;
            case 5:
                str = d.getBbs_cdn();
                break;
            case 6:
                str = d.getSdk();
                break;
            case 7:
                str = d.getBbs();
                break;
            case 8:
                str = d.getRecord();
                break;
            case 9:
                str = d.getCustomer();
                break;
            case 10:
                str = d.getBbs_event();
                break;
            case 11:
                str = d.getCoverageReport();
                break;
            case Y.v /* 12 */:
                str = d.getTeamBuild();
                break;
            case 13:
                str = d.getRnOffline();
                break;
            default:
                throw new J();
        }
        return Xe.n.a.n() ? Xe.t.a.r(str) : str;
    }

    @G50.l
    public final EnumC0868b c() {
        return d;
    }

    @G50.l
    public final String[] d() {
        synchronized (this) {
            String[] strArr = e;
            if (strArr != null) {
                return strArr;
            }
            String[] a2 = a.a();
            e = a2;
            return a2;
        }
    }

    public final boolean e() {
        return (d == EnumC0868b.ONLINE || d == EnumC0868b.BETA) ? false : true;
    }

    public final boolean f(String str) {
        HashSet hashSet = new HashSet();
        hashSet.add(d.getLogin());
        hashSet.add(d.getTakumi());
        hashSet.add(d.getBbs_cdn());
        hashSet.add(d.getBbs());
        if (Xe.t.a.n() && hashSet.contains("https://devapi-takumi.miyoushe.com")) {
            hashSet.add("https://dev-api.mihoyo.com");
            hashSet.remove("https://devapi-takumi.miyoushe.com");
        }
        if (hashSet.isEmpty()) {
            return false;
        }
        Iterator it = hashSet.iterator();
        while (it.hasNext()) {
            if (P.n3(str, (String) it.next(), false, 2, (Object) null)) {
                return true;
            }
        }
        return false;
    }

    public final boolean g(@G50.l String str) {
        K.p(str, "url");
        return f(str);
    }

    public final void h(@G50.l EnumC0868b enumC0868b) {
        K.p(enumC0868b, "value");
        d = enumC0868b;
        e = null;
    }
}
