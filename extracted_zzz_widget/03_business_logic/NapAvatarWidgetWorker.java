package com.mihoyo.hyperion.biz.miniwidget.napavatar;

import Cy.G;
import Cy.S;
import Cy.o0;
import G50.l;
import Oy.p;
import Ym.A;
import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.widget.RemoteViews;
import androidx.work.WorkerParameters;
import com.mihoyo.commlib.utils.LogUtils;
import com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.NapWidgetData;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.a;
import com.mihoyo.hyperion.net.model.bean.CommonResponseInfo;
import com.mihoyo.hyperion.utils.ExtensionsKt;
import hg.k;
import hg.m;
import java.util.HashMap;
import java.util.List;
import kotlin.jvm.functions.Function0;
import kotlin.jvm.functions.Function1;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import pg.C0774d;
import x0.v;
import zy.N0;
import zy.V;
import zy.f0;

@v(parameters = 1)
@r0({"SMAP\nNapAvatarWidgetWorker.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapAvatarWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetWorker\n+ 2 Context.kt\nandroidx/core/content/ContextKt\n+ 3 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,712:1\n31#2:713\n774#3:714\n865#3,2:715\n1869#3,2:717\n1878#3,3:719\n*S KotlinDebug\n*F\n+ 1 NapAvatarWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetWorker\n*L\n121#1:713\n372#1:714\n372#1:715,2\n375#1:717,2\n396#1:719,3\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapAvatarWidgetWorker extends BaseWidgetWorker {
    public static final int l = 0;
    public static final int m = 4;
    public static long o;

    @l
    public static final b k = new b(null);

    @l
    public static final List<String> n = G.k("Job was cancelled");
    public static final int p = m.C0090m.V2;
    public static final int q = m.h.y2;

    @v(parameters = 0)
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a {
        public static final int i = 8;

        @l
        public final Context a;

        @l
        public final String b;

        @l
        public final String c;
        public final int d;

        @l
        public final NapWidgetData.a.EnumC0040a e;
        public final int f;
        public final int g;
        public final boolean h;

        public a(@l Context context, @l String str, @l String str2, int i2, @l NapWidgetData.a.EnumC0040a enumC0040a, int i3, int i4, boolean z) {
            K.p(context, "context");
            K.p(str, "iconUrl");
            K.p(str2, "name");
            K.p(enumC0040a, "state");
            this.a = context;
            this.b = str;
            this.c = str2;
            this.d = i2;
            this.e = enumC0040a;
            this.f = i3;
            this.g = i4;
            this.h = z;
        }

        @l
        public final Context a() {
            return this.a;
        }

        @l
        public final String b() {
            return this.b;
        }

        @l
        public final String c() {
            return this.c;
        }

        public final int d() {
            return this.d;
        }

        @l
        public final NapWidgetData.a.EnumC0040a e() {
            return this.e;
        }

        public boolean equals(@G50.m Object obj) {
            if (this == obj) {
                return true;
            }
            if (!(obj instanceof a)) {
                return false;
            }
            a aVar = (a) obj;
            return K.g(this.a, aVar.a) && K.g(this.b, aVar.b) && K.g(this.c, aVar.c) && this.d == aVar.d && this.e == aVar.e && this.f == aVar.f && this.g == aVar.g && this.h == aVar.h;
        }

        public final int f() {
            return this.f;
        }

        public final int g() {
            return this.g;
        }

        public final boolean h() {
            return this.h;
        }

        public int hashCode() {
            return (((((((((((((this.a.hashCode() * 31) + this.b.hashCode()) * 31) + this.c.hashCode()) * 31) + Integer.hashCode(this.d)) * 31) + this.e.hashCode()) * 31) + Integer.hashCode(this.f)) * 31) + Integer.hashCode(this.g)) * 31) + Boolean.hashCode(this.h);
        }

        @l
        public final a i(@l Context context, @l String str, @l String str2, int i2, @l NapWidgetData.a.EnumC0040a enumC0040a, int i3, int i4, boolean z) {
            K.p(context, "context");
            K.p(str, "iconUrl");
            K.p(str2, "name");
            K.p(enumC0040a, "state");
            return new a(context, str, str2, i2, enumC0040a, i3, i4, z);
        }

        @l
        public final Context k() {
            return this.a;
        }

        public final int l() {
            return this.f;
        }

        @l
        public final String m() {
            return this.b;
        }

        @l
        public final String n() {
            return this.c;
        }

        @l
        public final NapWidgetData.a.EnumC0040a o() {
            return this.e;
        }

        public final int p() {
            return this.d;
        }

        public final int q() {
            return this.g;
        }

        public final boolean r() {
            return this.h;
        }

        @l
        public String toString() {
            return "ActivityViewData(context=" + this.a + ", iconUrl=" + this.b + ", name=" + this.c + ", timeSeconds=" + this.d + ", state=" + this.e + ", currentState=" + this.f + ", totalState=" + this.g + ", unlock=" + this.h + ")";
        }
    }

    @r0({"SMAP\nNapAvatarWidgetWorker.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapAvatarWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetWorker$Companion\n+ 2 fake.kt\nkotlin/jvm/internal/FakeKt\n+ 3 Extensions.kt\ncom/mihoyo/hyperion/biz/miniwidget/ExtensionsKt\n*L\n1#1,712:1\n1#2:713\n79#3,11:714\n*S KotlinDebug\n*F\n+ 1 NapAvatarWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetWorker$Companion\n*L\n87#1:714,11\n*E\n"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class b {
        public /* synthetic */ b(DefaultConstructorMarker defaultConstructorMarker) {
            this();
        }

        public static /* synthetic */ void e(b bVar, RemoteViews remoteViews, Context context, int i, String str, String str2, int i2, Object obj) {
            if ((i2 & 8) != 0) {
                str2 = "";
            }
            bVar.d(remoteViews, context, i, str, str2);
        }

        public final void c(@l Context context, @G50.m AppWidgetManager appWidgetManager, int i) {
            K.p(context, "context");
            if (appWidgetManager == null) {
                appWidgetManager = AppWidgetManager.getInstance(context);
            }
            RemoteViews remoteViews = new RemoteViews(context.getPackageName(), NapAvatarWidgetWorker.p);
            b bVar = NapAvatarWidgetWorker.k;
            bVar.f(remoteViews);
            e(bVar, remoteViews, context, m.j.Fl, C0774d.b, null, 8, null);
            e(bVar, remoteViews, context, m.j.Sk, C0774d.f, null, 8, null);
            e(bVar, remoteViews, context, m.j.a8, C0774d.b, null, 8, null);
            e(bVar, remoteViews, context, m.j.Em, C0774d.g, null, 8, null);
            appWidgetManager.updateAppWidget(i, remoteViews);
        }

        public final void d(RemoteViews remoteViews, Context context, int i, String str, String str2) {
            Bundle bundle;
            if (str2.length() > 0) {
                bundle = new Bundle();
                bundle.putString("url", str2);
            } else {
                bundle = null;
            }
            Intent intent = new Intent(context, (Class<?>) NapAvatarWidgetProvider.class);
            intent.setAction(str);
            if (bundle != null) {
                intent.putExtras(bundle);
            }
            remoteViews.setOnClickPendingIntent(i, PendingIntent.getBroadcast(context, 0, intent, 201326592));
        }

        public final void f(RemoteViews remoteViews) {
            remoteViews.setViewVisibility(m.j.zp, 8);
            remoteViews.setViewVisibility(m.j.Rc, 8);
            remoteViews.setViewVisibility(m.j.Em, 8);
            remoteViews.setViewVisibility(m.j.Ap, 8);
            remoteViews.setViewVisibility(m.j.fe, 8);
            remoteViews.setViewVisibility(m.j.u5, 8);
            remoteViews.setViewVisibility(m.j.a8, 8);
            remoteViews.setViewVisibility(m.j.Le, 0);
        }

        public final void g(RemoteViews remoteViews) {
            remoteViews.setViewVisibility(m.j.zp, 0);
            remoteViews.setViewVisibility(m.j.Rc, 0);
            remoteViews.setViewVisibility(m.j.Em, 0);
            remoteViews.setViewVisibility(m.j.Ap, 0);
            remoteViews.setViewVisibility(m.j.fe, 0);
            remoteViews.setViewVisibility(m.j.u5, 0);
            remoteViews.setViewVisibility(m.j.Le, 4);
        }

        public b() {
        }
    }

    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public /* synthetic */ class c {
        public static final /* synthetic */ int[] a;

        static {
            int[] iArr = new int[NapWidgetData.a.EnumC0040a.values().length];
            try {
                iArr[NapWidgetData.a.EnumC0040a.STATE_NOT_START.ordinal()] = 1;
            } catch (NoSuchFieldError unused) {
            }
            try {
                iArr[NapWidgetData.a.EnumC0040a.STATE_IN_PROGRESS.ordinal()] = 2;
            } catch (NoSuchFieldError unused2) {
            }
            try {
                iArr[NapWidgetData.a.EnumC0040a.STATE_COMPLETED.ordinal()] = 3;
            } catch (NoSuchFieldError unused3) {
            }
            a = iArr;
        }
    }

    @Oy.f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker", f = "NapAvatarWidgetWorker.kt", i = {0, 0, 0}, l = {589}, m = "createActivityView", n = {"data", "$this$createActivityView_u24lambda_u246", "isLast"}, s = {"L$0", "L$2", "Z$0"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class d extends Oy.d {
        public Object a;
        public Object b;
        public Object c;
        public boolean d;
        public /* synthetic */ Object e;
        public int g;

        public d(Ly.f<? super d> fVar) {
            super(fVar);
        }

        @G50.m
        public final Object invokeSuspend(@l Object obj) {
            this.e = obj;
            this.g |= Integer.MIN_VALUE;
            return NapAvatarWidgetWorker.this.Q(null, false, this);
        }
    }

    @Oy.f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker", f = "NapAvatarWidgetWorker.kt", i = {0, 0, 0, 0}, l = {546}, m = "showErrorUi", n = {"context", "firstMsg", "secondMsg", "$this$showErrorUi_u24lambda_u245"}, s = {"L$0", "L$1", "L$2", "L$4"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class e extends Oy.d {
        public Object a;
        public Object b;
        public Object c;
        public Object d;
        public Object e;
        public /* synthetic */ Object f;
        public int h;

        public e(Ly.f<? super e> fVar) {
            super(fVar);
        }

        @G50.m
        public final Object invokeSuspend(@l Object obj) {
            this.f = obj;
            this.h |= Integer.MIN_VALUE;
            return NapAvatarWidgetWorker.this.U(null, 0, this);
        }
    }

    @Oy.f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker", f = "NapAvatarWidgetWorker.kt", i = {0, 0}, l = {505}, m = "showNetworkErrorUi", n = {"context", "$this$showNetworkErrorUi_u24lambda_u244"}, s = {"L$0", "L$2"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class f extends Oy.d {
        public Object a;
        public Object b;
        public Object c;
        public /* synthetic */ Object d;
        public int f;

        public f(Ly.f<? super f> fVar) {
            super(fVar);
        }

        @G50.m
        public final Object invokeSuspend(@l Object obj) {
            this.d = obj;
            this.f |= Integer.MIN_VALUE;
            return NapAvatarWidgetWorker.this.V(null, this);
        }
    }

    @Oy.f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker", f = "NapAvatarWidgetWorker.kt", i = {0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 3, 4}, l = {154, 163, 172, 189, 203}, m = "update", n = {"this", "context", "tag", "appWidgetManager", "widgetIds", "startTime", "this", "context", "tag", "appWidgetManager", "widgetIds", "startTime", "this", "context", "tag", "appWidgetManager", "widgetIds", "message", "startTime", "this", "tag", "message"}, s = {"L$0", "L$1", "L$2", "L$3", "L$4", "J$0", "L$0", "L$1", "L$2", "L$3", "L$4", "J$0", "L$0", "L$1", "L$2", "L$3", "L$4", "L$5", "J$0", "L$0", "L$1", "L$0"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class g extends Oy.d {
        public Object a;
        public Object b;
        public Object c;
        public Object d;
        public Object e;
        public Object f;
        public Object g;
        public Object h;
        public long i;
        public /* synthetic */ Object j;
        public int l;

        public g(Ly.f<? super g> fVar) {
            super(fVar);
        }

        @G50.m
        public final Object invokeSuspend(@l Object obj) {
            this.j = obj;
            this.l |= Integer.MIN_VALUE;
            return NapAvatarWidgetWorker.this.I(null, this);
        }
    }

    @r0({"SMAP\nNapAvatarWidgetWorker.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapAvatarWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetWorker$update$data$1\n+ 2 RetrofitClient.kt\ncom/mihoyo/hyperion/net/RetrofitClient\n*L\n1#1,712:1\n161#2:713\n*S KotlinDebug\n*F\n+ 1 NapAvatarWidgetWorker.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/NapAvatarWidgetWorker$update$data$1\n*L\n155#1:713\n*E\n"})
    @Oy.f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker$update$data$1", f = "NapAvatarWidgetWorker.kt", i = {}, l = {156}, m = "invokeSuspend", n = {}, s = {})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class h extends p implements Function1<Ly.f<? super NapWidgetData>, Object> {
        public int a;

        public h(Ly.f<? super h> fVar) {
            super(1, fVar);
        }

        /* renamed from: b, reason: merged with bridge method [inline-methods] */
        public final Object invoke(Ly.f<? super NapWidgetData> fVar) {
            return create(fVar).invokeSuspend(N0.a);
        }

        public final Ly.f<N0> create(Ly.f<?> fVar) {
            return new h(fVar);
        }

        public final Object invokeSuspend(Object obj) {
            Object l = Ny.d.l();
            int i = this.a;
            if (i == 0) {
                f0.n(obj);
                com.mihoyo.hyperion.biz.miniwidget.napavatar.a aVar = (com.mihoyo.hyperion.biz.miniwidget.napavatar.a) A.a.n(com.mihoyo.hyperion.biz.miniwidget.napavatar.a.class);
                this.a = 1;
                obj = a.C0041a.a(aVar, null, this, 1, null);
                if (obj == l) {
                    return l;
                }
            } else {
                if (i != 1) {
                    throw new IllegalStateException("call to 'resume' before 'invoke' with coroutine");
                }
                f0.n(obj);
            }
            return ((CommonResponseInfo) obj).getData();
        }
    }

    @Oy.f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker", f = "NapAvatarWidgetWorker.kt", i = {0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 2}, l = {448, 465, 479}, m = "updateBgUi", n = {"$this$updateBgUi", "context", "avatarImageWidth", "coverColor", "$this$updateBgUi", "context", "avatarImageWidth", "coverColor", "$this$updateBgUi", "avatarBitmap", "bgColorBitmap", "avatarImageWidth", "coverColor"}, s = {"L$0", "L$1", "I$0", "I$1", "L$0", "L$1", "I$0", "I$1", "L$0", "L$1", "L$2", "I$0", "I$1"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class i extends Oy.d {
        public Object a;
        public Object b;
        public Object c;
        public int d;
        public int e;
        public /* synthetic */ Object f;
        public int h;

        public i(Ly.f<? super i> fVar) {
            super(fVar);
        }

        @G50.m
        public final Object invokeSuspend(@l Object obj) {
            this.f = obj;
            this.h |= Integer.MIN_VALUE;
            return NapAvatarWidgetWorker.this.W(null, null, this);
        }
    }

    @Oy.f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker", f = "NapAvatarWidgetWorker.kt", i = {0, 0, 0, 0, 0, 0}, l = {397, 430}, m = "updateContentUi", n = {"this", "context", "data", "$this$updateContentUi_u24lambda_u243", "showData", "index$iv"}, s = {"L$0", "L$1", "L$2", "L$4", "L$5", "I$0"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class j extends Oy.d {
        public Object a;
        public Object b;
        public Object c;
        public Object d;
        public Object e;
        public Object f;
        public Object g;
        public int h;
        public /* synthetic */ Object i;
        public int k;

        public j(Ly.f<? super j> fVar) {
            super(fVar);
        }

        @G50.m
        public final Object invokeSuspend(@l Object obj) {
            this.i = obj;
            this.k |= Integer.MIN_VALUE;
            return NapAvatarWidgetWorker.this.X(null, null, this);
        }
    }

    /* JADX WARN: 'super' call moved to the top of the method (can break code semantics) */
    public NapAvatarWidgetWorker(@l Context context, @l WorkerParameters workerParameters) {
        super(context, workerParameters);
        K.p(context, "context");
        K.p(workerParameters, "workerParameters");
    }

    private final void S(String str) {
        if (S.Y1(n, str)) {
            return;
        }
        LogUtils.INSTANCE.d("widget-nap-avatar", "updateContentUi error: " + str);
        Op.a.e("rpg_calendar_widget_update_crash", o0.M(new V[]{zy.r0.a("error_message", ExtensionsKt.ifNullOrBlank(str, new Function0() { // from class: pg.c
            public final Object invoke() {
                String T;
                T = NapAvatarWidgetWorker.T();
                return T;
            }
        }))}), new HashMap());
        ig.c cVar = ig.c.a;
        k kVar = k.NAP_AVATAR;
        if (str == null) {
            str = "";
        }
        cVar.f(kVar, false, str);
    }

    public static final String T() {
        return "";
    }

    /* JADX WARN: Can't wrap try/catch for region: R(17:105|106|107|(4:108|109|110|(2:111|112))|(3:122|123|(3:125|126|(1:128)(12:129|43|44|45|46|47|48|49|50|19|20|21)))|114|115|116|117|46|47|48|49|50|19|20|21) */
    /* JADX WARN: Can't wrap try/catch for region: R(17:1|(2:3|(14:5|6|(1:(1:(1:(1:(1:(9:13|14|15|16|17|18|19|20|21)(2:25|26))(9:27|28|29|30|31|32|19|20|21))(15:39|40|41|42|43|44|45|46|47|48|49|50|19|20|21))(15:94|95|96|97|98|99|100|101|102|103|49|50|19|20|21))(4:147|148|149|150))(2:180|(2:182|183)(2:184|(2:186|187)(2:188|(2:190|191)(2:192|(2:194|195)(7:196|197|198|199|200|201|(1:203)(1:204))))))|151|152|153|154|155|156|157|158|160|161|(1:163)(12:164|98|99|100|101|102|103|49|50|19|20|21)))|212|6|(0)(0)|151|152|153|154|155|156|157|158|160|161|(0)(0)|(1:(0))) */
    /* JADX WARN: Code restructure failed: missing block: B:119:0x034f, code lost:
    
        r0 = e;
     */
    /* JADX WARN: Code restructure failed: missing block: B:120:0x0350, code lost:
    
        r10 = r2;
        r2 = r12;
     */
    /* JADX WARN: Code restructure failed: missing block: B:166:0x0294, code lost:
    
        r0 = th;
     */
    /* JADX WARN: Code restructure failed: missing block: B:167:0x0295, code lost:
    
        r15 = r29;
        r9 = r4;
        r4 = r5;
        r5 = r6;
        r2 = r10;
        r13 = r11;
     */
    /* JADX WARN: Code restructure failed: missing block: B:168:0x035f, code lost:
    
        r0 = e;
     */
    /* JADX WARN: Code restructure failed: missing block: B:169:0x0360, code lost:
    
        r2 = r34;
        r27 = r8;
     */
    /* JADX WARN: Code restructure failed: missing block: B:170:0x0364, code lost:
    
        r8 = r28;
        r15 = r29;
        r9 = r4;
        r4 = r5;
        r13 = r11;
        r5 = r12;
     */
    /* JADX WARN: Code restructure failed: missing block: B:172:0x036d, code lost:
    
        r0 = e;
     */
    /* JADX WARN: Code restructure failed: missing block: B:173:0x036e, code lost:
    
        r2 = r34;
     */
    /* JADX WARN: Code restructure failed: missing block: B:175:0x0371, code lost:
    
        r0 = e;
     */
    /* JADX WARN: Code restructure failed: missing block: B:176:0x0372, code lost:
    
        r2 = "keep current ui because successful data exists";
     */
    /* JADX WARN: Code restructure failed: missing block: B:56:0x03a2, code lost:
    
        r34 = r2;
     */
    /* JADX WARN: Code restructure failed: missing block: B:57:0x03c7, code lost:
    
        if (Cy.H.O(new java.lang.Integer[]{Oy.b.f(-100), Oy.b.f(hg.h.d)}).contains(Oy.b.f(((ne.a) r0).a())) != false) goto L214;
     */
    /* JADX WARN: Code restructure failed: missing block: B:60:0x0431, code lost:
    
        r1 = "network error: " + ((ne.a) r0).a() + " " + r0.getMessage();
     */
    /* JADX WARN: Code restructure failed: missing block: B:66:0x04a2, code lost:
    
        r3.a = r1;
        r3.b = r4;
        r3.c = r9;
        r3.d = null;
        r3.e = null;
        r3.f = null;
        r3.g = null;
        r3.h = null;
        r3.l = 5;
        r0 = r10.V(r13, r3);
     */
    /* JADX WARN: Code restructure failed: missing block: B:67:0x04b9, code lost:
    
        if (r0 == r14) goto L158;
     */
    /* JADX WARN: Code restructure failed: missing block: B:68:0x04bb, code lost:
    
        return r14;
     */
    /* JADX WARN: Code restructure failed: missing block: B:69:0x04bc, code lost:
    
        r3 = r1;
        r2 = r9;
     */
    /* JADX WARN: Code restructure failed: missing block: B:71:0x04c5, code lost:
    
        r3 = r1;
     */
    /* JADX WARN: Code restructure failed: missing block: B:73:0x0456, code lost:
    
        r1 = r0.getMessage();
     */
    /* JADX WARN: Code restructure failed: missing block: B:74:0x045a, code lost:
    
        if (r1 == null) goto L151;
     */
    /* JADX WARN: Code restructure failed: missing block: B:75:0x045c, code lost:
    
        r1 = r15;
     */
    /* JADX WARN: Code restructure failed: missing block: B:77:0x03c9, code lost:
    
        r0 = ((ne.a) r0).a();
        r3.a = r10;
        r3.b = r5;
        r3.c = r4;
        r3.d = r9;
        r3.e = null;
        r3.f = null;
        r3.g = null;
        r3.h = null;
        r3.l = 4;
        r0 = r10.U(r13, r0, r3);
     */
    /* JADX WARN: Code restructure failed: missing block: B:78:0x03e7, code lost:
    
        if (r0 == r14) goto L136;
     */
    /* JADX WARN: Code restructure failed: missing block: B:79:0x03e9, code lost:
    
        return r14;
     */
    /* JADX WARN: Code restructure failed: missing block: B:80:0x03ea, code lost:
    
        r2 = r9;
        r3 = r10;
     */
    /* JADX WARN: Code restructure failed: missing block: B:82:0x03fd, code lost:
    
        r0 = th;
     */
    /* JADX WARN: Code restructure failed: missing block: B:83:0x03fe, code lost:
    
        r3 = r10;
     */
    /* JADX WARN: Code restructure failed: missing block: B:84:0x042c, code lost:
    
        r34 = r2;
     */
    /* JADX WARN: Removed duplicated region for block: B:122:0x02d2 A[EXC_TOP_SPLITTER, SYNTHETIC] */
    /* JADX WARN: Removed duplicated region for block: B:163:0x024e A[RETURN] */
    /* JADX WARN: Removed duplicated region for block: B:164:0x024f  */
    /* JADX WARN: Removed duplicated region for block: B:180:0x0135  */
    /* JADX WARN: Removed duplicated region for block: B:56:0x03a2  */
    /* JADX WARN: Removed duplicated region for block: B:60:0x0431  */
    /* JADX WARN: Removed duplicated region for block: B:63:0x0498  */
    /* JADX WARN: Removed duplicated region for block: B:73:0x0456  */
    /* JADX WARN: Removed duplicated region for block: B:84:0x042c  */
    /* JADX WARN: Removed duplicated region for block: B:8:0x003b  */
    @Override // com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker
    @G50.m
    @android.annotation.SuppressLint({"CheckResult"})
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public java.lang.Object I(@G50.l android.content.Context r33, @G50.l Ly.f<? super java.lang.Boolean> r34) {
        /*
            Method dump skipped, instructions count: 1243
            To view this dump add '--comments-level debug' option
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.I(android.content.Context, Ly.f):java.lang.Object");
    }

    /* JADX WARN: Removed duplicated region for block: B:13:0x009f  */
    /* JADX WARN: Removed duplicated region for block: B:16:0x00da  */
    /* JADX WARN: Removed duplicated region for block: B:21:0x0175  */
    /* JADX WARN: Removed duplicated region for block: B:24:0x0253  */
    /* JADX WARN: Removed duplicated region for block: B:28:0x0183  */
    /* JADX WARN: Removed duplicated region for block: B:43:0x013d  */
    /* JADX WARN: Removed duplicated region for block: B:46:0x0048  */
    /* JADX WARN: Removed duplicated region for block: B:9:0x002b  */
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public final java.lang.Object Q(com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.a r17, boolean r18, Ly.f<? super android.widget.RemoteViews> r19) {
        /*
            Method dump skipped, instructions count: 600
            To view this dump add '--comments-level debug' option
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.Q(com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker$a, boolean, Ly.f):java.lang.Object");
    }

    public final RemoteViews R(Context context) {
        return new RemoteViews(context.getPackageName(), m.C0090m.Q2);
    }

    /* JADX WARN: Removed duplicated region for block: B:12:0x00e8  */
    /* JADX WARN: Removed duplicated region for block: B:18:0x004f  */
    /* JADX WARN: Removed duplicated region for block: B:8:0x002b  */
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public final java.lang.Object U(android.content.Context r18, int r19, Ly.f<? super android.widget.RemoteViews> r20) {
        /*
            Method dump skipped, instructions count: 284
            To view this dump add '--comments-level debug' option
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.U(android.content.Context, int, Ly.f):java.lang.Object");
    }

    /* JADX WARN: Removed duplicated region for block: B:15:0x003d  */
    /* JADX WARN: Removed duplicated region for block: B:8:0x0023  */
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public final java.lang.Object V(android.content.Context r11, Ly.f<? super android.widget.RemoteViews> r12) {
        /*
            r10 = this;
            boolean r0 = r12 instanceof com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.f
            if (r0 == 0) goto L13
            r0 = r12
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker$f r0 = (com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.f) r0
            int r1 = r0.f
            r2 = -2147483648(0xffffffff80000000, float:-0.0)
            r3 = r1 & r2
            if (r3 == 0) goto L13
            int r1 = r1 - r2
            r0.f = r1
            goto L18
        L13:
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker$f r0 = new com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker$f
            r0.<init>(r12)
        L18:
            java.lang.Object r12 = r0.d
            java.lang.Object r1 = Ny.d.l()
            int r2 = r0.f
            r3 = 1
            if (r2 == 0) goto L3d
            if (r2 != r3) goto L35
            java.lang.Object r11 = r0.c
            android.widget.RemoteViews r11 = (android.widget.RemoteViews) r11
            java.lang.Object r1 = r0.b
            android.widget.RemoteViews r1 = (android.widget.RemoteViews) r1
            java.lang.Object r0 = r0.a
            android.content.Context r0 = (android.content.Context) r0
            zy.f0.n(r12)
            goto L6b
        L35:
            java.lang.IllegalStateException r11 = new java.lang.IllegalStateException
            java.lang.String r12 = "call to 'resume' before 'invoke' with coroutine"
            r11.<init>(r12)
            throw r11
        L3d:
            zy.f0.n(r12)
            ig.a r12 = ig.C0595a.a
            java.lang.String r2 = "widget-nap-avatar"
            java.lang.String r4 = "showNetworkErrorUi"
            r12.b(r2, r4)
            android.widget.RemoteViews r12 = new android.widget.RemoteViews
            java.lang.String r2 = r11.getPackageName()
            int r4 = com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.p
            r12.<init>(r2, r4)
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker$b r2 = com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.k
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.b.b(r2, r12)
            r0.a = r11
            r0.b = r12
            r0.c = r12
            r0.f = r3
            java.lang.Object r0 = r10.W(r12, r11, r0)
            if (r0 != r1) goto L68
            return r1
        L68:
            r0 = r11
            r11 = r12
            r1 = r11
        L6b:
            int r12 = hg.m.j.Nm
            r2 = 8
            r11.setViewVisibility(r12, r2)
            int r12 = hg.m.j.Ap
            r11.setViewVisibility(r12, r2)
            int r12 = hg.m.j.fe
            r11.setViewVisibility(r12, r2)
            int r12 = hg.m.j.u5
            r11.setViewVisibility(r12, r2)
            int r12 = hg.m.j.a8
            r2 = 0
            r11.setViewVisibility(r12, r2)
            int r12 = hg.m.j.vq
            java.lang.String r3 = "数据加载失败"
            r11.setTextViewText(r12, r3)
            int r12 = hg.m.j.sm
            r11.setViewVisibility(r12, r2)
            int r12 = hg.m.j.um
            java.lang.String r2 = "点击刷新或进入App查看"
            r11.setTextViewText(r12, r2)
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker$b r12 = com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.k
            int r5 = hg.m.j.Fl
            r8 = 8
            r9 = 0
            java.lang.String r6 = "com.mihoyo.hyperion.widget.napavatar.ACTION_OPEN_APP"
            r7 = 0
            r2 = r12
            r3 = r11
            r4 = r0
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.b.e(r2, r3, r4, r5, r6, r7, r8, r9)
            int r5 = hg.m.j.a8
            java.lang.String r6 = "com.mihoyo.hyperion.widget.napavatar.ACTION_OPEN_APP"
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.b.e(r2, r3, r4, r5, r6, r7, r8, r9)
            int r5 = hg.m.j.Em
            java.lang.String r6 = "com.mihoyo.hyperion.widget.napavatar.ACTION_OPEN_SETTING"
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.b.e(r2, r3, r4, r5, r6, r7, r8, r9)
            int r5 = hg.m.j.Sk
            java.lang.String r6 = "com.mihoyo.hyperion.widget.napavatar.ACTION_REFRESH"
            com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.b.e(r2, r3, r4, r5, r6, r7, r8, r9)
            return r1
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.V(android.content.Context, Ly.f):java.lang.Object");
    }

    /* JADX WARN: Removed duplicated region for block: B:15:0x01d9  */
    /* JADX WARN: Removed duplicated region for block: B:18:0x01dc  */
    /* JADX WARN: Removed duplicated region for block: B:26:0x0159  */
    /* JADX WARN: Removed duplicated region for block: B:29:0x01bb A[RETURN] */
    /* JADX WARN: Removed duplicated region for block: B:30:0x01bc  */
    /* JADX WARN: Removed duplicated region for block: B:31:0x0164  */
    /* JADX WARN: Removed duplicated region for block: B:36:0x00ff A[EXC_TOP_SPLITTER, SYNTHETIC] */
    /* JADX WARN: Removed duplicated region for block: B:40:0x007e  */
    /* JADX WARN: Removed duplicated region for block: B:9:0x0033  */
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public final java.lang.Object W(android.widget.RemoteViews r21, android.content.Context r22, Ly.f<? super zy.N0> r23) {
        /*
            Method dump skipped, instructions count: 484
            To view this dump add '--comments-level debug' option
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.W(android.widget.RemoteViews, android.content.Context, Ly.f):java.lang.Object");
    }

    /* JADX WARN: Removed duplicated region for block: B:19:0x0341  */
    /* JADX WARN: Removed duplicated region for block: B:32:0x0386  */
    /* JADX WARN: Removed duplicated region for block: B:35:0x0410 A[RETURN] */
    /* JADX WARN: Removed duplicated region for block: B:36:0x0411  */
    /* JADX WARN: Removed duplicated region for block: B:38:0x0398  */
    /* JADX WARN: Removed duplicated region for block: B:39:0x0066  */
    /* JADX WARN: Removed duplicated region for block: B:8:0x002b  */
    /* JADX WARN: Unsupported multi-entry loop pattern (BACK_EDGE: B:28:0x0371 -> B:16:0x0377). Please report as a decompilation issue!!! */
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public final java.lang.Object X(android.content.Context r32, com.mihoyo.hyperion.biz.miniwidget.napavatar.NapWidgetData r33, Ly.f<? super android.widget.RemoteViews> r34) {
        /*
            Method dump skipped, instructions count: 1043
            To view this dump add '--comments-level debug' option
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetWorker.X(android.content.Context, com.mihoyo.hyperion.biz.miniwidget.napavatar.NapWidgetData, Ly.f):java.lang.Object");
    }
}
