package com.mihoyo.hyperion.biz.miniwidget.napavatar.settings;

import G50.l;
import G50.m;
import Ny.d;
import Oy.f;
import Oy.p;
import Tx.g;
import Ym.A;
import ZZ.T;
import ZZ.i;
import ZZ.m0;
import androidx.lifecycle.S;
import androidx.lifecycle.Y;
import androidx.lifecycle.w0;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground;
import java.util.List;
import kotlin.Lazy;
import kotlin.enums.EnumEntries;
import kotlin.jvm.functions.Function0;
import kotlin.jvm.functions.Function2;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.r0;
import rg.C0792a;
import x0.v;
import zy.G;
import zy.N0;
import zy.V;
import zy.f0;

@v(parameters = 0)
@r0({"SMAP\nNapSettingsViewModel.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapSettingsViewModel.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/settings/NapSettingsViewModel\n+ 2 RetrofitClient.kt\ncom/mihoyo/hyperion/net/RetrofitClient\n*L\n1#1,71:1\n161#2:72\n*S KotlinDebug\n*F\n+ 1 NapSettingsViewModel.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/settings/NapSettingsViewModel\n*L\n29#1:72\n*E\n"})
/* loaded from: X:\AI project\解包\tools\decompiled_res\resources\classes12.dex */
public final class a extends w0 {
    public static final int h = 8;

    @l
    public final Y<List<AvatarBackground.a>> a;

    @l
    public final S<List<AvatarBackground.a>> b;

    @l
    public final Y<V<List<AvatarBackground.b>, List<AvatarBackground.b>>> c;

    @l
    public final S<V<List<AvatarBackground.b>, List<AvatarBackground.b>>> d;

    @l
    public final Y<EnumC0042a> e;

    @l
    public final S<EnumC0042a> f;

    @l
    public final Lazy g;

    /* JADX WARN: Failed to restore enum class, 'enum' modifier and super class removed */
    /* JADX WARN: Unknown enum class pattern. Please report as an issue! */
    /* renamed from: com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a$a, reason: collision with other inner class name */
    /* loaded from: X:\AI project\解包\tools\decompiled_res\resources\classes12.dex */
    public static final class EnumC0042a {
        private static final /* synthetic */ EnumEntries $ENTRIES;
        private static final /* synthetic */ EnumC0042a[] $VALUES;
        public static final EnumC0042a LOADING = new EnumC0042a("LOADING", 0);
        public static final EnumC0042a ERROR = new EnumC0042a("ERROR", 1);
        public static final EnumC0042a SUCCESS = new EnumC0042a("SUCCESS", 2);

        private static final /* synthetic */ EnumC0042a[] $values() {
            return new EnumC0042a[]{LOADING, ERROR, SUCCESS};
        }

        static {
            EnumC0042a[] $values = $values();
            $VALUES = $values;
            $ENTRIES = Py.b.c($values);
        }

        private EnumC0042a(String str, int i) {
        }

        @l
        public static EnumEntries<EnumC0042a> getEntries() {
            return $ENTRIES;
        }

        public static EnumC0042a valueOf(String str) {
            return (EnumC0042a) Enum.valueOf(EnumC0042a.class, str);
        }

        public static EnumC0042a[] values() {
            return (EnumC0042a[]) $VALUES.clone();
        }
    }

    @f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.NapSettingsViewModel$getCacheBgInfo$2", f = "NapSettingsViewModel.kt", i = {}, l = {}, m = "invokeSuspend", n = {}, s = {})
    /* loaded from: X:\AI project\解包\tools\decompiled_res\resources\classes12.dex */
    public static final class b extends p implements Function2<T, Ly.f<? super AvatarBackground.a>, Object> {
        public int a;

        public b(Ly.f<? super b> fVar) {
            super(2, fVar);
        }

        public final Ly.f<N0> create(Object obj, Ly.f<?> fVar) {
            return new b(fVar);
        }

        public final Object invoke(T t, Ly.f<? super AvatarBackground.a> fVar) {
            return create(t, fVar).invokeSuspend(N0.a);
        }

        public final Object invokeSuspend(Object obj) {
            d.l();
            if (this.a != 0) {
                throw new IllegalStateException("call to 'resume' before 'invoke' with coroutine");
            }
            f0.n(obj);
            return C0792a.a.c();
        }
    }

    @r0({"SMAP\nNapSettingsViewModel.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapSettingsViewModel.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/settings/NapSettingsViewModel$getImageSettingList$1\n+ 2 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,71:1\n774#2:72\n865#2,2:73\n1869#2,2:75\n*S KotlinDebug\n*F\n+ 1 NapSettingsViewModel.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/settings/NapSettingsViewModel$getImageSettingList$1\n*L\n37#1:72\n37#1:73,2\n46#1:75,2\n*E\n"})
    @f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.NapSettingsViewModel$getImageSettingList$1", f = "NapSettingsViewModel.kt", i = {0, 1, 1}, l = {35, 39}, m = "invokeSuspend", n = {"$this$launchNetWork", "$this$launchNetWork", "resultData"}, s = {"L$0", "L$0", "L$1"})
    /* loaded from: X:\AI project\解包\tools\decompiled_res\resources\classes12.dex */
    public static final class c extends p implements Function2<T, Ly.f<? super N0>, Object> {
        public Object a;
        public int b;
        public /* synthetic */ Object c;

        public c(Ly.f<? super c> fVar) {
            super(2, fVar);
        }

        public final Ly.f<N0> create(Object obj, Ly.f<?> fVar) {
            c cVar = new c(fVar);
            cVar.c = obj;
            return cVar;
        }

        public final Object invoke(T t, Ly.f<? super N0> fVar) {
            return create(t, fVar).invokeSuspend(N0.a);
        }

        /* JADX WARN: Removed duplicated region for block: B:12:0x00c4  */
        /* JADX WARN: Removed duplicated region for block: B:20:0x00db A[EDGE_INSN: B:20:0x00db->B:16:0x00db BREAK  A[LOOP:0: B:10:0x00be->B:19:?], SYNTHETIC] */
        /* JADX WARN: Removed duplicated region for block: B:8:0x00b6  */
        /*
            Code decompiled incorrectly, please refer to instructions dump.
            To view partially-correct add '--show-bad-code' argument
        */
        public final java.lang.Object invokeSuspend(java.lang.Object r10) {
            /*
                r9 = this;
                java.lang.Object r0 = Ny.d.l()
                int r1 = r9.b
                r2 = 2
                r3 = 1
                if (r1 == 0) goto L2b
                if (r1 == r3) goto L23
                if (r1 != r2) goto L1b
                java.lang.Object r0 = r9.a
                java.util.List r0 = (java.util.List) r0
                java.lang.Object r1 = r9.c
                ZZ.T r1 = (ZZ.T) r1
                zy.f0.n(r10)
                goto Lab
            L1b:
                java.lang.IllegalStateException r10 = new java.lang.IllegalStateException
                java.lang.String r0 = "call to 'resume' before 'invoke' with coroutine"
                r10.<init>(r0)
                throw r10
            L23:
                java.lang.Object r1 = r9.c
                ZZ.T r1 = (ZZ.T) r1
                zy.f0.n(r10)
                goto L4f
            L2b:
                zy.f0.n(r10)
                java.lang.Object r10 = r9.c
                r1 = r10
                ZZ.T r1 = (ZZ.T) r1
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.this
                androidx.lifecycle.Y r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.j(r10)
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a$a r4 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.EnumC0042a.LOADING
                r10.r(r4)
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.this
                com.mihoyo.hyperion.biz.miniwidget.napavatar.a r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.h(r10)
                r9.c = r1
                r9.b = r3
                java.lang.Object r10 = r10.b(r9)
                if (r10 != r0) goto L4f
                return r0
            L4f:
                com.mihoyo.hyperion.net.model.bean.CommonResponseInfo r10 = (com.mihoyo.hyperion.net.model.bean.CommonResponseInfo) r10
                java.lang.Object r10 = r10.getData()
                com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground r10 = (com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground) r10
                java.util.ArrayList r4 = new java.util.ArrayList
                r4.<init>()
                java.util.List r5 = r10.getAvatarSkins()
                java.lang.Iterable r5 = (java.lang.Iterable) r5
                java.util.ArrayList r6 = new java.util.ArrayList
                r6.<init>()
                java.util.Iterator r5 = r5.iterator()
            L6b:
                boolean r7 = r5.hasNext()
                if (r7 == 0) goto L82
                java.lang.Object r7 = r5.next()
                r8 = r7
                com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground$a r8 = (com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground.a) r8
                boolean r8 = r8.r()
                if (r8 == 0) goto L6b
                r6.add(r7)
                goto L6b
            L82:
                r4.addAll(r6)
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a r5 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.this
                androidx.lifecycle.Y r5 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.k(r5)
                zy.V r6 = new zy.V
                java.util.List r7 = r10.getElementTypes()
                java.util.List r10 = r10.getAvatarProfessions()
                r6.<init>(r7, r10)
                r5.r(r6)
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.this
                r9.c = r1
                r9.a = r4
                r9.b = r2
                java.lang.Object r10 = r10.n(r9)
                if (r10 != r0) goto Laa
                return r0
            Laa:
                r0 = r4
            Lab:
                com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground$a r10 = (com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground.a) r10
                rg.a r1 = rg.C0792a.a
                pg.b r1 = r1.b()
                if (r10 == 0) goto Lb6
                goto Lb7
            Lb6:
                r10 = r1
            Lb7:
                r1 = r0
                java.lang.Iterable r1 = (java.lang.Iterable) r1
                java.util.Iterator r1 = r1.iterator()
            Lbe:
                boolean r2 = r1.hasNext()
                if (r2 == 0) goto Ldb
                java.lang.Object r2 = r1.next()
                com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground$a r2 = (com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground.a) r2
                java.lang.String r4 = r2.getId()
                java.lang.String r5 = r10.getId()
                boolean r4 = kotlin.jvm.internal.K.g(r4, r5)
                if (r4 == 0) goto Lbe
                r2.a(r3)
            Ldb:
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.this
                androidx.lifecycle.Y r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.i(r10)
                r10.r(r0)
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.this
                androidx.lifecycle.Y r10 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.j(r10)
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a$a r0 = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.EnumC0042a.SUCCESS
                r10.r(r0)
                zy.N0 r10 = zy.N0.a
                return r10
            */
            throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.c.invokeSuspend(java.lang.Object):java.lang.Object");
        }
    }

    public a() {
        Y<List<AvatarBackground.a>> y = new Y<>();
        this.a = y;
        this.b = y;
        Y<V<List<AvatarBackground.b>, List<AvatarBackground.b>>> y2 = new Y<>();
        this.c = y2;
        this.d = y2;
        Y<EnumC0042a> y3 = new Y<>();
        this.e = y3;
        this.f = y3;
        this.g = G.b(new Function0() { // from class: qg.m
            public final Object invoke() {
                com.mihoyo.hyperion.biz.miniwidget.napavatar.a l;
                l = com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.l();
                return l;
            }
        });
    }

    public static final com.mihoyo.hyperion.biz.miniwidget.napavatar.a l() {
        return (com.mihoyo.hyperion.biz.miniwidget.napavatar.a) A.a.n(com.mihoyo.hyperion.biz.miniwidget.napavatar.a.class);
    }

    public static final void q(a aVar, Throwable th2) {
        new gn.l((Function2) null, 1, (DefaultConstructorMarker) null).d(th2);
        aVar.e.r(EnumC0042a.ERROR);
    }

    public final com.mihoyo.hyperion.biz.miniwidget.napavatar.a m() {
        return (com.mihoyo.hyperion.biz.miniwidget.napavatar.a) this.g.getValue();
    }

    @m
    public final Object n(@l Ly.f<? super AvatarBackground.a> fVar) {
        return i.h(m0.c(), new b(null), fVar);
    }

    @l
    public final S<List<AvatarBackground.a>> o() {
        return this.b;
    }

    public final void p() {
        Tg.b.d(this, new c(null), new g() { // from class: qg.l
            public final void accept(Object obj) {
                com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.q(com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.this, (Throwable) obj);
            }
        });
    }

    @l
    public final S<EnumC0042a> r() {
        return this.f;
    }

    @l
    public final S<V<List<AvatarBackground.b>, List<AvatarBackground.b>>> s() {
        return this.d;
    }
}
