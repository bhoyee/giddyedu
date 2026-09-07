import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ThemedText } from '@/components/themed-text';
import { ThemedView } from '@/components/themed-view';
import { AccessContext, getAccessContext, hasStoredSession, signIn, signOut } from '@/lib/auth-client';

const features = [
  { label: 'Children', permission: 'Students.View', entitlement: 'student-information' },
  { label: 'Guardians', permission: 'Guardians.View', entitlement: 'guardian-management' },
];

export default function FamilyHome() {
  const [email,setEmail]=useState(''); const [password,setPassword]=useState(''); const [tenantId,setTenantId]=useState(''); const [busy,setBusy]=useState(true); const [message,setMessage]=useState('Restoring your secure session…'); const [access,setAccess]=useState<AccessContext|null>(null);
  useEffect(()=>{ void (async()=>{ try { if (await hasStoredSession()) { setAccess(await getAccessContext()); setMessage('Your secure session was restored.'); } else setMessage('Sign in to securely load your permitted family workspace.'); } catch(error) { await signOut(); setMessage(error instanceof Error?error.message:'Your session could not be restored.'); } finally { setBusy(false); } })(); },[]);
  async function login() { setBusy(true); try { await signIn(email,password,tenantId); setAccess(await getAccessContext()); setPassword(''); setMessage('Connected. Features are controlled by your school access.'); } catch(error) { setMessage(error instanceof Error?error.message:'Sign-in failed.'); } finally { setBusy(false); } }
  async function logout() { await signOut(); setAccess(null); setPassword(''); setMessage('You have signed out securely.'); }
  const hasFamilyAudience=access?.audiences.some(value=>value==='Parent'||value==='Student')===true;
  const available=features.filter(feature=>hasFamilyAudience&&access?.permissions.includes(feature.permission)&&access.entitlements[feature.entitlement]?.enabled);
  return <ThemedView style={styles.screen}><SafeAreaView style={styles.safe}><View style={styles.header}><ThemedText type="title">Your family</ThemedText><ThemedText>One secure place for every child&apos;s school journey.</ThemedText></View>{busy&&!access?<ActivityIndicator/>:!access?<View style={styles.card}><TextInput autoCapitalize="none" keyboardType="email-address" placeholder="Email" value={email} onChangeText={setEmail} style={styles.input}/><TextInput placeholder="Password" secureTextEntry value={password} onChangeText={setPassword} style={styles.input}/><TextInput autoCapitalize="none" placeholder="School workspace ID" value={tenantId} onChangeText={setTenantId} style={styles.input}/><Pressable disabled={busy||!email||!password||!tenantId} onPress={()=>void login()} style={styles.button}>{busy?<ActivityIndicator color="#fff"/>:<ThemedText style={styles.buttonText}>Sign in</ThemedText>}</Pressable></View>:<View style={styles.card}><ThemedText type="subtitle">Family access connected</ThemedText><View style={styles.row}>{available.length?available.map(feature=><View key={feature.label} style={styles.tile}><ThemedText type="smallBold">{feature.label}</ThemedText><ThemedText type="small">Enabled by your school</ThemedText></View>):<ThemedText type="small">No Family features are currently enabled for this account.</ThemedText>}</View><Pressable onPress={()=>void logout()} style={styles.secondary}><ThemedText>Sign out</ThemedText></Pressable></View>}<ThemedText type="small">{message}</ThemedText></SafeAreaView></ThemedView>;
}
const styles=StyleSheet.create({screen:{flex:1},safe:{flex:1,padding:24,gap:20},header:{gap:8,paddingTop:32},card:{backgroundColor:'#DFF3E7',borderRadius:24,padding:22,gap:12},input:{backgroundColor:'#fff',borderColor:'#94A3B8',borderWidth:1,borderRadius:12,padding:14,color:'#0F172A'},button:{backgroundColor:'#166534',borderRadius:12,padding:14,alignItems:'center'},buttonText:{color:'#fff'},secondary:{borderWidth:1,borderColor:'#64748B',borderRadius:12,padding:12,alignItems:'center'},row:{gap:12},tile:{borderWidth:1,borderColor:'#CBD5E1',borderRadius:18,padding:18,gap:4}});
